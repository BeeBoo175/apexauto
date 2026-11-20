using System.IdentityModel.Tokens.Jwt;
using Dealership.Api.Configuration;
using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using Dealership.Application.Photos;
using Dealership.Domain.Intake;
using Dealership.Domain.Photos;
using Microsoft.AspNetCore.Http.Features;

namespace Dealership.Api.Endpoints;

public static class IntakePhotoEndpoints
{
    public static void MapIntakePhotoEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/intake/photos").RequireAuthorization();
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            context.HttpContext.Response.Headers.XContentTypeOptions = "nosniff";
            return await next(context);
        });
        routes.MapPut("/{id:guid}", UploadAsync).RequireRateLimiting(ApiRateLimiting.PhotoTransferPolicy);
        routes.MapGet("/{id:guid}/receipt", GetReceiptAsync);
        routes.MapGet("/{id:guid}", DownloadAsync).RequireRateLimiting(ApiRateLimiting.PhotoTransferPolicy);
    }

    private static async Task<IResult> UploadAsync(Guid id, string? angle, HttpContext context,
        IntakePhotoService photos, IStaffAssistanceQuery assistance, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PhotoAngle>(angle, out var selectedAngle) || !Enum.IsDefined(selectedAngle) ||
            selectedAngle.ToString() != angle || id == Guid.Empty)
        {
            return Results.Problem(statusCode: 400, title: "Use an upload UUID and Driver, Passenger, Front or Rear angle.");
        }

        var contentType = context.Request.ContentType?.Split(';', 2)[0].Trim().ToLowerInvariant();

        if (!PhotoContent.IsSupportedContentType(contentType))
        {
            return Results.Problem(statusCode: 415, title: "Upload a JPEG, PNG or WebP photo.");
        }

        var owner = await AssistedOwner.ResolveAsync(context, assistance, cancellationToken);
        if (owner.Error is not null)
        {
            return owner.Error;
        }

        if (!await photos.CanUploadAsync(owner.OwnerId, cancellationToken))
        {
            return Results.Forbid();
        }

        var sizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();

        if (sizeFeature is { IsReadOnly: false })
        {
            sizeFeature.MaxRequestBodySize = PhotoTemporaryFile.UploadMaximumBytes;
        }

        if (context.Request.ContentLength > PhotoTemporaryFile.UploadMaximumBytes)
        {
            return Results.Problem(statusCode: 413, title: PhotoTemporaryFile.UploadTooLargeMessage);
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(90));

        try
        {
            await using var file = PhotoTemporaryFile.Create();
            using var bodyDeadline = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            bodyDeadline.CancelAfter(TimeSpan.FromSeconds(60));
            await PhotoTemporaryFile.CopyRequestAsync(context.Request.Body, file, bodyDeadline.Token);
            return Map(await photos.UploadAsync(owner.OwnerId, id, selectedAngle, contentType!, file, deadline.Token, owner.Staff?.Id));
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
            return Results.Problem(statusCode: 408, title: "Upload deadline exceeded; reconcile using the same upload ID.");
        }
    }

    private static async Task<IResult> GetReceiptAsync(Guid id, HttpContext context, IntakePhotoService photos,
        IStaffAssistanceQuery assistance, CancellationToken cancellationToken)
    {
        var owner = await AssistedOwner.ResolveAsync(context, assistance, cancellationToken);
        if (owner.Error is not null)
        {
            return owner.Error;
        }

        return Map(await photos.GetReceiptAsync(owner.OwnerId, id, cancellationToken, owner.Staff?.Id));
    }

    private static async Task<IResult> DownloadAsync(Guid id, HttpContext context, IntakePhotoService photos,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        await using var file = PhotoTemporaryFile.Create();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40), clock);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        try
        {
            var result = await photos.DownloadAsync(OwnerId(context), id, file, deadline.Token);

            if (result.Status != PhotoStatus.Available)
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

    private static Guid OwnerId(HttpContext context) => Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    private static IResult Map(PhotoResult result)
    {
        return result.Status switch
        {
            PhotoStatus.Available => Results.Ok(result.Receipt),
            PhotoStatus.Pending => Results.Json(result.Receipt, statusCode: 202),
            PhotoStatus.DeletionPending or PhotoStatus.Deleted => Results.Json(result.Receipt, statusCode: 410),
            PhotoStatus.Forbidden => Results.Forbid(),
            PhotoStatus.Conflict => Results.Problem(statusCode: 409, title: "Upload ID already belongs to different photo contents or angle."),
            PhotoStatus.NotFound => Results.NotFound(),
            _ => Results.Problem(statusCode: 400, title: "Photo contents are invalid, animated or exceed the decoded pixel limit.")
        };
    }

    private static IResult StorageUnavailable() => Results.Problem(statusCode: 503,
        title: "Photo storage is unavailable; reconcile using the same upload ID and contents.");
}
