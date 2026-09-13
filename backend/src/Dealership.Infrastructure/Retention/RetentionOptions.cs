namespace Dealership.Infrastructure.Retention;

// Technical limits of the routine minimization worker; the durations are Administrator settings, not configuration.
public sealed class RetentionOptions
{
    public const int DefaultBatchSize = 25;

    public bool Enabled { get; set; } = true;

    public int BatchSize { get; set; } = DefaultBatchSize;
}
