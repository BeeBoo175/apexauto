using System.IdentityModel.Tokens.Jwt;
using Dealership.Application.Inspections;
using Dealership.Application.Photos;
using Dealership.Domain.Inspections;
using Dealership.Domain.Photos;
using Microsoft.AspNetCore.Http.Features;

namespace Dealership.Api.Endpoints;

public static class InspectionPhotoEndpoints
{
    public static void MapInspectionPhotoEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/staff/inspection/drafts/{inspectionId:guid}/photos").RequireAuthorization();
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            context.HttpContext.Response.Headers.XContentTypeOptions = "nosniff";
            return await next(context);
        });
        routes.MapPut("/{id:guid}", UploadAsync).RequireRateLimiting(IntakePhotoEndpoints.TransferPolicy);
        routes.MapGet("/{id:guid}/receipt", ReceiptAsync);
        routes.MapGet("/{id:guid}", DownloadAsync).RequireRateLimiting(IntakePhotoEndpoints.TransferPolicy);
    }

    private static async Task<IResult> UploadAsync(Guid inspectionId, Guid id, string? pointCode,
        long assignmentVersion, HttpContext context, InspectionPhotoService photos, CancellationToken cancellationToken)
    {
        if (inspectionId == Guid.Empty || id == Guid.Empty || assignmentVersion < 1 ||
            !InspectionCatalog.Points.Any(point => point.Code == pointCode))
        {
            return Results.Problem(statusCode: 400, title: "Provide an inspection, upload UUID, catalog point and assignment version.");
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
            var access = await photos.CheckUploadAsync(ActorId(context), inspectionId, assignmentVersion, deadline.Token);
            if (access != InspectionPhotoStatus.Available)
            {
                return Map(new(access));
            }

            var size = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (size is { IsReadOnly: false })
            {
                size.MaxRequestBodySize = PhotoContent.MaximumBytes;
            }
            if (context.Request.ContentLength > PhotoContent.MaximumBytes)
            {
                return Results.Problem(statusCode: 413, title: "Photo exceeds 10 MiB.");
            }

            await using var file = PhotoTemporaryFile.Create();
            using var bodyDeadline = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            bodyDeadline.CancelAfter(TimeSpan.FromSeconds(60));
            await PhotoTemporaryFile.CopyRequestAsync(context.Request.Body, file, bodyDeadline.Token);
            return Map(await photos.UploadAsync(ActorId(context), inspectionId, id, assignmentVersion,
                pointCode!, contentType!, file, deadline.Token));
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

    private static async Task<IResult> ReceiptAsync(Guid inspectionId, Guid id, HttpContext context,
        InspectionPhotoService photos, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            return Map(await photos.GetReceiptAsync(ActorId(context), inspectionId, id, deadline.Token));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StorageUnavailable();
        }
    }

    private static async Task<IResult> DownloadAsync(Guid inspectionId, Guid id, HttpContext context,
        InspectionPhotoService photos, CancellationToken cancellationToken)
    {
        await using var file = PhotoTemporaryFile.Create();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(40));
        try
        {
            var result = await photos.DownloadAsync(ActorId(context), inspectionId, id, file, deadline.Token);
            if (result.Status != InspectionPhotoStatus.Available)
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

    private static Guid ActorId(HttpContext context) => Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    private static IResult Map(InspectionPhotoResult result)
    {
        return result.Status switch
        {
            InspectionPhotoStatus.Available => Results.Ok(result.Receipt),
            InspectionPhotoStatus.Pending => Results.Json(result.Receipt, statusCode: 202),
            InspectionPhotoStatus.Gone => Results.Json(result.Receipt, statusCode: 410),
            InspectionPhotoStatus.Forbidden => Results.Forbid(),
            InspectionPhotoStatus.NotFound => Results.NotFound(),
            InspectionPhotoStatus.Conflict => Results.Problem(statusCode: 409, title: "The inspection assignment or upload contents changed."),
            _ => Results.Problem(statusCode: 400, title: "Photo contents are invalid, animated or exceed the decoded pixel limit.")
        };
    }

    private static IResult StorageUnavailable() => Results.Problem(statusCode: 503,
        title: "Photo storage is unavailable; reconcile with the same upload ID and contents.");
}
