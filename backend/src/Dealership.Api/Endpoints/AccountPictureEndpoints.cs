using Dealership.Api.Configuration;
using Dealership.Application.Accounts;
using Dealership.Application.Photos;
using Dealership.Domain.Photos;
using Microsoft.AspNetCore.Http.Features;

namespace Dealership.Api.Endpoints;

public static class AccountPictureEndpoints
{
    public static void MapAccountPictureEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/accounts").RequireAuthorization().NoStore();
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.XContentTypeOptions = "nosniff";
            return await next(context);
        });
        routes.MapPut("/me/picture/{pictureId:guid}", UploadAsync).RequireRateLimiting(ApiRateLimiting.PhotoTransferPolicy);
        routes.MapGet("/me/picture/{pictureId:guid}/receipt", GetReceiptAsync);
        routes.MapGet("/{id:guid}/picture", DownloadAsync).RequireRateLimiting(ApiRateLimiting.PictureDownloadPolicy);
        routes.MapDelete("/{id:guid}/picture/{pictureId:guid}", RemoveAsync);
    }

    private static async Task<IResult> UploadAsync(Guid pictureId, HttpContext context, AccountPictureService pictures,
        CancellationToken cancellationToken)
    {
        var contentType = context.Request.ContentType?.Split(';', 2)[0].Trim().ToLowerInvariant();

        if (!PhotoContent.IsSupportedContentType(contentType))
        {
            return Results.Problem(statusCode: 415, title: "Upload a JPEG, PNG or WebP picture.");
        }

        var sizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();

        if (sizeFeature is { IsReadOnly: false })
        {
            sizeFeature.MaxRequestBodySize = PhotoContent.MaximumBytes;
        }

        if (context.Request.ContentLength > PhotoContent.MaximumBytes)
        {
            return Results.Problem(statusCode: 413, title: "Picture exceeds 10 MiB.");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(90));

        try
        {
            await using var file = PhotoTemporaryFile.Create();
            using var bodyDeadline = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            bodyDeadline.CancelAfter(TimeSpan.FromSeconds(60));
            await PhotoTemporaryFile.CopyRequestAsync(context.Request.Body, file, bodyDeadline.Token);
            return Map(await pictures.UploadAsync(HttpContextActor.ActorId(context), pictureId, contentType!, file, deadline.Token));
        }
        catch (BadHttpRequestException exception)
        {
            return Results.Problem(statusCode: exception.StatusCode);
        }
        catch (PhotoStorageException)
        {
            return StorageUnavailable();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 408, title: "Upload deadline exceeded; reconcile using the same picture ID.");
        }
    }

    private static async Task<IResult> GetReceiptAsync(Guid pictureId, HttpContext context, AccountPictureService pictures,
        CancellationToken cancellationToken)
    {
        return Map(await pictures.GetReceiptAsync(HttpContextActor.ActorId(context), pictureId, cancellationToken));
    }

    private static async Task<IResult> DownloadAsync(Guid id, HttpContext context, AccountPictureService pictures,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        await using var file = PhotoTemporaryFile.Create();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40), clock);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        try
        {
            var result = await pictures.DownloadAsync(HttpContextActor.ActorId(context), id, file, deadline.Token);

            if (result.Status != AccountPictureStatus.Available)
            {
                return Map(result);
            }

            file.Position = 0;
            context.Response.ContentType = result.Receipt!.ContentType;
            context.Response.ContentLength = file.Length;
            await context.Response.StartAsync(deadline.Token);
            await file.CopyToAsync(context.Response.Body, deadline.Token);
            return Results.Empty;
        }
        catch (PhotoStorageException)
        {
            return StorageUnavailable();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (context.Response.HasStarted)
            {
                context.Abort();
                return Results.Empty;
            }

            context.Response.ContentType = null;
            context.Response.ContentLength = null;
            return StorageUnavailable();
        }
    }

    private static async Task<IResult> RemoveAsync(Guid id, Guid pictureId, HttpContext context, AccountPictureService pictures,
        CancellationToken cancellationToken)
    {
        try
        {
            return MapRemoval(await pictures.RemoveAsync(HttpContextActor.ActorId(context), id, pictureId, cancellationToken));
        }
        catch (PhotoStorageException)
        {
            return StorageUnavailable();
        }
    }

    private static IResult Map(AccountPictureResult result)
    {
        return result.Status switch
        {
            AccountPictureStatus.Available => Results.Ok(result.Receipt),
            AccountPictureStatus.Pending => Results.Json(result.Receipt, statusCode: 202),
            AccountPictureStatus.DeletionPending or AccountPictureStatus.Deleted => Results.Json(result.Receipt, statusCode: 410),
            AccountPictureStatus.Forbidden => Results.Forbid(),
            AccountPictureStatus.Conflict => Results.Problem(statusCode: 409,
                title: "The picture ID already belongs to different contents or another account."),
            AccountPictureStatus.NotFound => Results.NotFound(),
            _ => Results.Problem(statusCode: 400, title: "Picture contents are invalid, animated or exceed the decoded pixel limit.")
        };
    }

    // The receipt of the removed picture answers a first removal and every replay of it.
    private static IResult MapRemoval(AccountPictureResult result)
    {
        return result.Status switch
        {
            AccountPictureStatus.DeletionPending or AccountPictureStatus.Deleted => Results.Ok(result.Receipt),
            AccountPictureStatus.Conflict => Results.Problem(statusCode: 409,
                title: "That picture is not the current one; reload the account."),
            _ => Map(result)
        };
    }

    private static IResult StorageUnavailable() => Results.Problem(statusCode: 503,
        title: "Picture storage is unavailable; try again.");
}
