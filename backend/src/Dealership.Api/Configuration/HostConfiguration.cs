using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Dealership.Api.Configuration;

public static class HostConfiguration
{
    public static string ResolveEnvironment(IConfiguration configuration)
    {
        return configuration["environment"] ?? Environments.Production;
    }

    public static void ValidateAllowedHosts(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("AllowedHosts requires a semicolon-separated list of explicit hosts.");
        }

        var hosts = value.Split(';', StringSplitOptions.TrimEntries);

        if (hosts.Any(host => host.Length == 0 || host.Contains('*') || host.Contains(',')))
        {
            throw new InvalidOperationException("AllowedHosts requires nonempty, explicit hosts separated by semicolons; wildcards are forbidden.");
        }
    }

    public static void ConfigureForwardedHeaders(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        foreach (var value in configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
        {
            if (!IPAddress.TryParse(value, out var address))
            {
                throw new InvalidOperationException("ForwardedHeaders:KnownProxies must contain valid proxy IP addresses.");
            }

            options.KnownProxies.Add(address);
        }

        // An empty trust list means trust everyone in the middleware, so disable forwarding instead.
        if (options.KnownProxies.Count == 0)
        {
            options.ForwardedHeaders = ForwardedHeaders.None;
        }
    }
}
