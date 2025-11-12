using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;

namespace Dealership.Api.Endpoints;

internal static class JsonRequestReader
{
    public const int MaximumRequestBytes = 16 * 1024;

    public static readonly TimeSpan BodyReadDeadline = TimeSpan.FromSeconds(10);

    public static async Task<(T? Value, IResult? Error)> ReadAsync<T>(HttpRequest request,
        CancellationToken cancellationToken) where T : class
    {
        if (!request.HasJsonContentType())
        {
            return (null, Results.Problem(statusCode: StatusCodes.Status415UnsupportedMediaType));
        }

        var sizeFeature = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();

        if (sizeFeature is { IsReadOnly: false })
        {
            sizeFeature.MaxRequestBodySize = MaximumRequestBytes;
        }

        if (request.ContentLength > MaximumRequestBytes)
        {
            return (null, Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge));
        }

        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(BodyReadDeadline);
            var value = await ReadBodyAsync<T>(request.Body, deadline.Token);
            return value is null ? (null, Results.Problem(statusCode: StatusCodes.Status400BadRequest)) : (value, null);
        }
        catch (JsonException)
        {
            return (null, Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid JSON request."));
        }
        catch (BadHttpRequestException exception)
        {
            return (null, Results.Problem(statusCode: exception.StatusCode));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (null, Results.Problem(statusCode: StatusCodes.Status408RequestTimeout));
        }
    }

    private static async Task<T?> ReadBodyAsync<T>(Stream body, CancellationToken cancellationToken)
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
            throw new BadHttpRequestException("JSON request is too large.", StatusCodes.Status413PayloadTooLarge);
        }

        return JsonSerializer.Deserialize<T>(buffer.AsSpan(0, length), JsonSerializerOptions.Web);
    }
}
