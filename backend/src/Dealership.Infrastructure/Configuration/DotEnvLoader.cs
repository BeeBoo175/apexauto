namespace Dealership.Infrastructure.Configuration;

public static class DotEnvLoader
{
    private const string EnvFileName = ".env";

    // Only for local development: deployed hosts must inject real environment variables.
    public static void LoadForDevelopment(string startDirectory)
    {
        var envPath = FindEnvFile(startDirectory)
            ?? throw new FileNotFoundException(
                $"Development requires a {EnvFileName} file at or above '{startDirectory}'. Copy .env.example to {EnvFileName}.");

        var lines = File.ReadAllLines(envPath);

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var trimmed = lines[lineIndex].Trim();

            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = trimmed.IndexOf('=');

            if (separatorIndex <= 0)
            {
                throw new FormatException($"Malformed line in {envPath} at line {lineIndex + 1}. Expected KEY=VALUE.");
            }

            var key = trimmed[..separatorIndex].Trim();
            var value = trimmed[(separatorIndex + 1)..].Trim();

            if (value.Length >= 2 && value.StartsWith('"') && value.EndsWith('"'))
            {
                value = value[1..^1];
            }

            // Real environment variables take precedence over the file.
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    private static string? FindEnvFile(string startDirectory)
    {
        for (var current = new DirectoryInfo(startDirectory); current != null; current = current.Parent)
        {
            var candidate = Path.Combine(current.FullName, EnvFileName);

            if (File.Exists(candidate))
            {
                return candidate;
            }

            // The repository root is the boundary; never read a .env outside it.
            if (Directory.Exists(Path.Combine(current.FullName, ".git")))
            {
                return null;
            }
        }

        return null;
    }
}
