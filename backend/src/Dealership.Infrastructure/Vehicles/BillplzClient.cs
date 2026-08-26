using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Configuration;

namespace Dealership.Infrastructure.Vehicles;

// The one place that builds Billplz addresses and credentials, sends a request and turns transport, status and JSON
// failures into PaymentGatewayException. The gateways keep their own fields and outcome mapping.
internal sealed class BillplzClient(HttpClient client, BillplzSettings settings)
{
    // Bills live under the configured root (v3).
    public Uri BillAddress(string path) => new(RootWithSlash(settings.BaseUrl), path);

    // Payment orders live under v5 of the same host the bills use.
    public Uri PaymentOrderAddress(string path)
    {
        var root = settings.BaseUrl.TrimEnd('/');
        var v5 = root.EndsWith("/v3", StringComparison.Ordinal) ? root[..^3] + "/v5" : root;
        return new Uri(RootWithSlash(v5), path);
    }

    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, Uri address, HttpContent? content, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(method, address) { Content = content };
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(settings.ApiKey + ":"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        try
        {
            var response = await client.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                response.Dispose();
                throw new PaymentGatewayException($"The payment provider answered {status}.");
            }

            return response;
        }
        catch (HttpRequestException exception)
        {
            throw new PaymentGatewayException("The payment provider could not be reached.", exception);
        }
    }

    public static async Task<JsonDocument> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new PaymentGatewayException("The payment provider could not be reached.", exception);
        }
        catch (JsonException exception)
        {
            throw new PaymentGatewayException("The payment provider returned an unreadable answer.", exception);
        }
    }

    // A root without its trailing slash would drop the last path segment when the path is appended.
    private static Uri RootWithSlash(string root) => new(root.EndsWith('/') ? root : root + '/');
}
