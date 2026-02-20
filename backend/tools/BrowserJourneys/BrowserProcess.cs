using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Dealership.BrowserJourneys;

internal static class BrowserProcess
{
    public static string AllocateNextOrigin()
    {
        using var port = new TcpListener(IPAddress.Loopback, 0);
        port.Start();
        return $"http://127.0.0.1:{((IPEndPoint)port.LocalEndpoint).Port}";
    }

    public static async Task<int> RunAsync(string frontend, string project, string[] arguments, string evidence,
        Uri apiOrigin, string browserOrigin, string nextOrigin, Dictionary<string, string>? scenario = null)
    {
        var start = new ProcessStartInfo("node") { WorkingDirectory = frontend, UseShellExecute = false };
        foreach (var argument in new[] { "node_modules/@playwright/test/cli.js", "test", "--config=playwright.config.ts", $"--project={project}" }.Concat(arguments))
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["API_BASE_URL"] = apiOrigin.GetLeftPart(UriPartial.Authority);
        start.Environment["APP_BASE_URL"] = browserOrigin;
        start.Environment["BROWSER_NEXT_ORIGIN"] = nextOrigin;
        start.Environment["TRUSTED_CLIENT_IP_HEADER"] = TrustedIngress.ClientHeader;
        start.Environment["BROWSER_EVIDENCE_DIR"] = evidence;
        start.Environment.Remove("BROWSER_INTAKE");
        start.Environment.Remove("BROWSER_RECOVERY_SCENARIO");
        start.Environment.Remove("NO_COLOR");
        start.Environment.Remove("FORCE_COLOR");
        foreach (var (name, value) in scenario ?? [])
        {
            start.Environment[name] = value;
        }

        using var browser = Process.Start(start) ?? throw new InvalidOperationException("Browser runner did not start.");
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        try
        {
            await browser.WaitForExitAsync(deadline.Token);
            return browser.ExitCode;
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            throw new TimeoutException("Selected browser journey exceeded its five-minute deadline.");
        }
        finally
        {
            if (!browser.HasExited)
            {
                browser.Kill(entireProcessTree: true);
                await browser.WaitForExitAsync();
            }
        }
    }
}
