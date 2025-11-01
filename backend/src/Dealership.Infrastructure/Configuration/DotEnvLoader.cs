namespace Dealership.Infrastructure.Configuration;

public static class DotEnvLoader
{
    public static void Load(string? explicitPath = null)
    {
        string? envPath = explicitPath;

        if (string.IsNullOrWhiteSpace(envPath))
        {
            var current = new DirectoryInfo(Directory.GetCurrentDirectory());

            while (current != null)
            {
                var candidate = Path.Combine(current.FullName, ".env");

                if (File.Exists(candidate))
                {
                    envPath = candidate;
                    break;
                }

                current = current.Parent;
            }
        }

        if (string.IsNullOrWhiteSpace(envPath) || !File.Exists(envPath))
        {
            return;
        }

        foreach (var line in File.ReadAllLines(envPath))
        {
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = trimmed.IndexOf('=');

            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = trimmed[..separatorIndex].Trim();
            var value = trimmed[(separatorIndex + 1)..].Trim();

            if (value.StartsWith('"') && value.EndsWith('"') && value.Length >= 2)
            {
                value = value[1..^1];
            }

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
