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
        routes.MapPut("/me/picture", UploadAsync).RequireRateLimiting(ApiRateLimiting.PhotoTransferPolicy);
        routes.MapGet("/{id:guid}/picture", DownloadAsync).RequireRateLimiting(ApiRateLimiting.PhotoTransferPolicy);
        routes.MapDelete("/{id:guid}/picture", RemoveAsync);
    }

    private static async Task<IResult> UploadAsync(HttpContext context, AccountPictureService pictures,
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
            return Map(await pictures.UploadAsync(HttpContextActor.ActorId(context), contentType!, file, deadline.Token));
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
            return Results.Problem(statusCode: 408, title: "Upload deadline exceeded; try again.");
        }
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
            context.Response.ContentType = result.ContentType;
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

    private static async Task<IResult> RemoveAsync(Guid id, HttpContext context, AccountPictureService pictures,
        CancellationToken cancellationToken)
    {
        try
        {
            return Map(await pictures.RemoveAsync(HttpContextActor.ActorId(context), id, cancellationToken));
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
            AccountPictureStatus.Stored => Results.Ok(new { pictureId = result.PictureId }),
            AccountPictureStatus.Removed => Results.NoContent(),
            AccountPictureStatus.Forbidden => Results.Forbid(),
            AccountPictureStatus.NotFound => Results.NotFound(),
            _ => Results.Problem(statusCode: 400, title: "Picture contents are invalid, animated or exceed the decoded pixel limit.")
        };
    }

    private static IResult StorageUnavailable() => Results.Problem(statusCode: 503,
        title: "Picture storage is unavailable; try again.");
}
