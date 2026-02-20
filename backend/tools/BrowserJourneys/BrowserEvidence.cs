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
