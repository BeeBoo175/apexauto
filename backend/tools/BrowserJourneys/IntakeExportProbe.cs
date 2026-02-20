using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Dealership.BrowserJourneys;

internal sealed class IntakeExportProbe(string directory) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.Use(async (context, continuation) =>
            {
                var fault = File.ReadAllText(Path.Combine(directory, "fault.txt"));
                if (context.Request.Path != "/api/staff/intake/appointments/export" ||
                    fault is not ("invalid-export" or "oversized-export" or "stalled-export"))
                {
                    await continuation();
                    return;
                }
                if (fault == "invalid-export")
                {
                    await context.Response.WriteAsJsonAsync(new { title = "Unexpected export content" });
                    return;
                }
                context.Response.ContentType = "text/csv; charset=utf-8";
                if (fault == "oversized-export")
                {
                    await context.Response.Body.WriteAsync(new byte[8 * 1024 * 1024 + 1], context.RequestAborted);
                    return;
                }
                await context.Response.WriteAsync("\uFEFF\"Registration\"\r\n", context.RequestAborted);
                await context.Response.Body.FlushAsync(context.RequestAborted);
                File.WriteAllText(Path.Combine(directory, "export-started.json"), "true");
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted);
                }
                catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
                {
                    File.WriteAllText(Path.Combine(directory, "export-cancelled.json"), "true");
                }
            });
            next(app);
        };
    }
}
