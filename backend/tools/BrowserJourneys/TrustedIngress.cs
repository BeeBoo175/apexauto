using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dealership.BrowserJourneys;

internal static class TrustedIngress
{
    public const string ClientHeader = "X-ApexAuto-Client-IP";

    public static async Task<WebApplication> StartAsync(string nextOrigin)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddHttpClient("next", client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        var app = builder.Build();
        app.Run(async context =>
        {
            var target = new Uri(new Uri(nextOrigin), context.Request.Path + context.Request.QueryString);
            using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), target);

            if (context.Request.ContentLength > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
            {
                request.Content = new StreamContent(context.Request.Body);
            }

            foreach (var header in context.Request.Headers)
            {
                if (IsForwardingOrHopHeader(header.Key))
                {
                    continue;
                }

                if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
                {
                    request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }

            var address = context.Connection.RemoteIpAddress!;
            request.Headers.Add(ClientHeader, address.IsIPv4MappedToIPv6 ? address.MapToIPv4().ToString() : address.ToString());
            var client = context.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("next");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
            context.Response.StatusCode = (int)response.StatusCode;

            foreach (var header in response.Headers.Concat(response.Content.Headers))
            {
                if (!IsForwardingOrHopHeader(header.Key))
                {
                    context.Response.Headers[header.Key] = header.Value.ToArray();
                }
            }

            await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
        });
        await app.StartAsync();
        return app;
    }

    public static string Origin(WebApplication app)
    {
        return app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    }

    private static bool IsForwardingOrHopHeader(string name)
    {
        return name.Equals(ClientHeader, StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Forwarded", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("X-Forwarded-", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("X-Original-", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Connection", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Keep-Alive", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Upgrade", StringComparison.OrdinalIgnoreCase);
    }
}
