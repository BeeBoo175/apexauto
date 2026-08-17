using Dealership.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dealership.Infrastructure.Identity;

public sealed class AuthMailLinks
{
    private readonly string _baseUrl;

    public AuthMailLinks(IConfiguration configuration, IOptions<EmailDeliverySettings> options,
        IHostEnvironment environment)
    {
        if (!Uri.TryCreate(configuration["APP_BASE_URL"], UriKind.Absolute, out var origin))
        {
            throw new InvalidOperationException("Authentication mail requires an absolute frontend URL.");
        }

        if (options.Value.Mode == "Loopback" && (!environment.IsDevelopment() ||
            origin.Host is not ("localhost" or "127.0.0.1")))
        {
            throw new InvalidOperationException("Loopback authentication mail requires Development and a local frontend.");
        }

        _baseUrl = configuration["APP_BASE_URL"]!.TrimEnd('/');
    }

    public string Create(string path, string token) => $"{_baseUrl}{path}#token={Uri.EscapeDataString(token)}";

    // A link without a token, for an address a payment provider returns the browser to.
    public string Absolute(string pathAndQuery) => $"{_baseUrl}{pathAndQuery}";
}
