namespace Dealership.BrowserJourneys;

internal static class BrowserJourneysCli
{
    // Mobile adds cost for every environment, so only the golden purchase journey runs on both devices.
    private const string GoldenMobileSpecification = "vehicles-ui-sales-completion.spec.ts";

    // The retention settings tab is an administrator page that is also checked at phone width, on that device only.
    private const string MobileOnlySpecification = "intake-ui-retention-mobile.spec.ts";

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help", StringComparer.Ordinal))
        {
            Console.WriteLine("BrowserJourneys <auth|intake|recovery> <spec filename|request|reset|fragment|--all> [--both]");
            Console.WriteLine($"Desktop only by default. --both adds mobile for the golden journey ({GoldenMobileSpecification}) only. No arguments start no services.");
            return 0;
        }

        var mode = args[0];
        if (mode is not ("auth" or "intake" or "recovery"))
        {
            Console.Error.WriteLine("Choose auth, intake or recovery. Use --help for usage.");
            return 2;
        }

        var repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.."));
        var specifications = Path.Combine(repository, "frontend", "tests", "e2e");
        var available = AvailableSelections(mode, specifications);
        var requested = args.Skip(1).Where(argument => argument != "--both").ToArray();
        if (requested.Length == 0 || (requested.Contains("--all") && requested.Length != 1))
        {
            Console.Error.WriteLine("Select a journey explicitly, or use --all for this group.");
            return 2;
        }

        var selected = requested[0] == "--all" ? available : requested.Distinct(StringComparer.Ordinal).ToArray();
        if (selected.Any(selection => !available.Contains(selection, StringComparer.Ordinal)))
        {
            Console.Error.WriteLine($"Available {mode} journeys: {string.Join(", ", available)}");
            return 2;
        }

        string[] projects = args.Contains("--both", StringComparer.Ordinal) ? ["desktop", "mobile"] : ["desktop"];
        foreach (var selection in selected)
        {
            var selectedProjects = ProjectsFor(selection, projects);
            var result = mode switch
            {
                "auth" => await AuthBrowserJourney.RunAsync(selection, selectedProjects),
                "intake" => await IntakeBrowserJourney.RunAsync(selection, selectedProjects),
                _ => await RecoveryBrowserJourney.RunAsync(selection, selectedProjects)
            };
            if (result != 0)
            {
                return result;
            }
        }
        return 0;
    }

    private static string[] ProjectsFor(string selection, string[] requested)
    {
        if (selection == MobileOnlySpecification)
        {
            return ["mobile"];
        }

        if (selection == GoldenMobileSpecification)
        {
            return requested;
        }

        return ["desktop"];
    }

    private static string[] AvailableSelections(string mode, string directory)
    {
        if (mode == "recovery")
        {
            return ["request", "reset", "fragment"];
        }

        var pattern = mode == "intake" ? "intake-ui*.spec.ts" : "*auth*.spec.ts";
        var files = Directory.GetFiles(directory, pattern);
        if (mode == "intake")
        {
            files = files.Concat(Directory.GetFiles(directory, "inspection-ui*.spec.ts"))
                .Concat(Directory.GetFiles(directory, "vehicles-ui*.spec.ts")).ToArray();
        }
        return files.Select(Path.GetFileName).OfType<string>()
            .Where(name => name != "browser-auth-password-recovery.spec.ts")
            .Order(StringComparer.Ordinal).ToArray();
    }
}
