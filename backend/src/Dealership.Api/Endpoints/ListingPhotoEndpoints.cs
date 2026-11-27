using Dealership.Api.Configuration;
using Dealership.Application.Vehicles;
using Dealership.Application.Photos;
using Dealership.Domain.Photos;
using Microsoft.AspNetCore.Http.Features;
using static Dealership.Api.Endpoints.HttpContextActor;

namespace Dealership.Api.Endpoints;

public static class ListingPhotoEndpoints
{
    public static void MapListingPhotoEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/staff/listings/{vehicleId:guid}/photos").RequireAuthorization();
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            context.HttpContext.Response.Headers.XContentTypeOptions = "nosniff";
            return await next(context);
        });
        routes.MapPut("/{id:guid}", UploadAsync).RequireRateLimiting(ApiRateLimiting.PhotoTransferPolicy);
        routes.MapGet("/{id:guid}/receipt", ReceiptAsync);
        routes.MapGet("/{id:guid}", (Guid vehicleId, Guid id, HttpContext context,
            ListingPhotoService photos, CancellationToken token) =>
                DownloadAsync(context, (file, deadline) =>
                    photos.DownloadAsync(ActorId(context), vehicleId, id, file, deadline), token))
            .RequireRateLimiting(ApiRateLimiting.PhotoReadPolicy);
        app.MapGet("/api/vehicles/{vehicleId:guid}/photos/{id:guid}", (Guid vehicleId, Guid id, HttpContext context,
            ListingPhotoService photos, CancellationToken token) =>
                DownloadAsync(context, (file, deadline) =>
                    photos.DownloadAsync(null, vehicleId, id, file, deadline), token))
            .RequireRateLimiting(ApiRateLimiting.PhotoReadPolicy);
        app.MapGet("/api/account/reservations/{reservationId:guid}/photos/{id:guid}",
            (Guid reservationId, Guid id, HttpContext context, ListingPhotoService photos, CancellationToken token) =>
                DownloadAsync(context, (file, deadline) =>
                    photos.DownloadReservationAsync(ActorId(context), reservationId, id, file, deadline), token))
            .RequireAuthorization().RequireRateLimiting(ApiRateLimiting.PhotoReadPolicy);
    }

    private static async Task<IResult> UploadAsync(Guid vehicleId, Guid id, HttpContext context, ListingPhotoService photos, CancellationToken cancellationToken)
    {
        if (vehicleId == Guid.Empty || id == Guid.Empty)
        {
            return Results.Problem(statusCode: 400, title: "Provide a vehicle and upload UUID.");
        }

        var contentType = context.Request.ContentType?.Split(';', 2)[0].Trim().ToLowerInvariant();
        if (!PhotoContent.IsSupportedContentType(contentType))
        {
            return Results.Problem(statusCode: 415, title: "Upload a JPEG, PNG or WebP photo.");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(90));
        try
        {
            var access = await photos.CheckUploadAsync(ActorId(context), vehicleId, deadline.Token);
            if (access != ListingPhotoStatus.Available)
            {
                return Map(new(access));
            }

            var size = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (size is { IsReadOnly: false })
            {
                size.MaxRequestBodySize = PhotoTemporaryFile.UploadMaximumBytes;
            }
            if (context.Request.ContentLength > PhotoTemporaryFile.UploadMaximumBytes)
            {
                return Results.Problem(statusCode: 413, title: PhotoTemporaryFile.UploadTooLargeMessage);
            }

            await using var file = PhotoTemporaryFile.Create();
            using var bodyDeadline = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            bodyDeadline.CancelAfter(TimeSpan.FromSeconds(60));
            await PhotoTemporaryFile.CopyRequestAsync(context.Request.Body, file, bodyDeadline.Token);
            return Map(await photos.UploadAsync(ActorId(context), vehicleId, id, contentType!, file, deadline.Token));
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
            return Results.Problem(statusCode: 408, title: "Upload is unconfirmed; reconcile with the same upload ID and contents.");
        }
    }

    private static async Task<IResult> ReceiptAsync(Guid vehicleId, Guid id, HttpContext context,
        ListingPhotoService photos, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            return Map(await photos.ReceiptAsync(ActorId(context), vehicleId, id, deadline.Token));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StorageUnavailable();
        }
    }

    internal static async Task<IResult> DownloadAsync(HttpContext context,
        Func<Stream, CancellationToken, Task<ListingPhotoResult>> download, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        await using var file = PhotoTemporaryFile.Create();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(40));
        try
        {
            var result = await download(file, deadline.Token);
            if (result.Status != ListingPhotoStatus.Available)
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

    private static IResult Map(ListingPhotoResult result)
    {
        return result.Status switch
        {
            ListingPhotoStatus.Available => Results.Ok(result.Receipt),
            ListingPhotoStatus.Pending => Results.Json(result.Receipt, statusCode: 202),
            ListingPhotoStatus.Removed => Results.Json(result.Receipt, statusCode: 410),
            ListingPhotoStatus.Forbidden => Results.Forbid(),
            ListingPhotoStatus.NotFound => Results.NotFound(),
            ListingPhotoStatus.Unavailable => StorageUnavailable(),
            ListingPhotoStatus.Conflict => Results.Problem(statusCode: 409, title: "The upload ID already belongs to different photo contents or ownership."),
            _ => Results.Problem(statusCode: 400, title: "Photo contents are invalid, animated or exceed the decoded pixel limit.")
        };
    }

    private static IResult StorageUnavailable() => Results.Problem(statusCode: 503,
        title: "Photo storage is unavailable; reconcile with the same upload ID and contents.");
}
