using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Dealership.BrowserJourneys;

internal sealed class BrowserEvidence(string directory) : IStartupFilter
{
    private readonly ConcurrentDictionary<string, int> _counts = new();
    private readonly object _snapshotLock = new();

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.Use(async (context, continuation) =>
            {
                var path = context.Request.Path.Value ?? string.Empty;
                var fault = File.ReadAllText(Path.Combine(directory, "fault.txt"));

                if (fault == "unavailable")
                {
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    await context.Response.WriteAsJsonAsync(new { title = "Unavailable" });
                    return;
                }

                _counts.AddOrUpdate(path, 1, (_, count) => count + 1);

                lock (_snapshotLock)
                {
                    File.WriteAllText(Path.Combine(directory, "counts.json"), JsonSerializer.Serialize(_counts));
                }

                var transportPath = path.StartsWith("/api/auth/", StringComparison.Ordinal) && path != "/api/auth/session" ||
                    path == "/api/intake/appointments";
                if (transportPath && fault.StartsWith("retry:", StringComparison.Ordinal))
                {
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    var retry = fault["retry:".Length..];
                    if (retry.Length > 0)
                    {
                        context.Response.Headers.RetryAfter = retry;
                    }
                    await context.Response.WriteAsJsonAsync(new { title = "private provider detail", retryAfterSeconds = 999999 });
                    return;
                }

                if (path == "/api/intake/appointments" && fault.StartsWith("conflict:", StringComparison.Ordinal))
                {
                    context.Response.StatusCode = StatusCodes.Status409Conflict;
                    await context.Response.WriteAsJsonAsync(new { code = fault["conflict:".Length..], title = "private provider detail" });
                    return;
                }

                if (path == "/api/intake/appointments" && fault == "invalid-success")
                {
                    await context.Response.WriteAsJsonAsync(new { title = "private provider detail" });
                    return;
                }

                if (fault == "invalid-login" && path == "/api/auth/login")
                {
                    await context.Response.WriteAsJsonAsync(new { accessToken = "invalid", expiresAt = DateTimeOffset.UtcNow.AddHours(1) });
                    return;
                }

                if (fault == "slow-login" && path == "/api/auth/login")
                {
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("{");
                    await context.Response.Body.FlushAsync();
                    await Task.Delay(TimeSpan.FromSeconds(15), context.RequestAborted);
                    return;
                }

                await continuation();
            });
            next(app);
        };
    }
}
