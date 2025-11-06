using Dealership.Application.Accounts;

namespace Dealership.Api.Endpoints;

public static class RegistrationEndpoints
{
    public const int MaximumRequestBytes = 16 * 1024;

    public static readonly TimeSpan BodyReadDeadline = TimeSpan.FromSeconds(10);

    public static void MapRegistrationEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/register", RegisterAsync);
    }

    private static async Task<IResult> RegisterAsync(HttpContext context, RegistrationService service,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";

        if (!context.Request.HasJsonContentType())
        {
            return Results.Problem(statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        var sizeFeature = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();

        if (sizeFeature is { IsReadOnly: false })
        {
            sizeFeature.MaxRequestBodySize = MaximumRequestBytes;
        }

        if (context.Request.ContentLength > MaximumRequestBytes)
        {
            return Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        RegistrationRequest? request;

        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(BodyReadDeadline);
            request = await ReadRequestAsync(context.Request.Body, deadline.Token);
        }
        catch (System.Text.Json.JsonException)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid registration request.");
        }
        catch (BadHttpRequestException exception)
        {
            return Results.Problem(statusCode: exception.StatusCode);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: StatusCodes.Status408RequestTimeout);
        }

        if (request is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await service.RegisterAsync(request, cancellationToken);

        return result.Status switch
        {
            RegistrationStatus.Succeeded => Results.NoContent(),
            RegistrationStatus.Conflict => Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Registration conflicts with an existing account or request. Use existing-account recovery."),
            _ => Results.ValidationProblem(result.Errors.ToDictionary())
        };
    }

    private static async Task<RegistrationRequest?> ReadRequestAsync(Stream body, CancellationToken cancellationToken)
    {
        // The extra byte detects oversized bodies even when Content-Length is absent.
        var buffer = new byte[MaximumRequestBytes + 1];
        var length = 0;

        while (length < buffer.Length)
        {
            var read = await body.ReadAsync(buffer.AsMemory(length), cancellationToken);

            if (read == 0)
            {
                break;
            }

            length += read;
        }

        if (length > MaximumRequestBytes)
        {
            throw new BadHttpRequestException("Registration request is too large.", StatusCodes.Status413PayloadTooLarge);
        }

        return System.Text.Json.JsonSerializer.Deserialize<RegistrationRequest>(buffer.AsSpan(0, length),
            System.Text.Json.JsonSerializerOptions.Web);
    }
}
