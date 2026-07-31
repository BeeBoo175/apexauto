namespace Dealership.Api.Configuration;

public sealed class RateLimitingSettings
{
    public const string SectionName = "RateLimiting";

    public int ApiRequestsPerWindow { get; init; } = ApiRateLimiting.RequestsPerMinute;

    public int PictureReadsPerWindow { get; init; } = ApiRateLimiting.PictureReadsPerMinute;

    public int LoginAttemptsPerWindow { get; init; } = ApiRateLimiting.LoginAttemptsPerMinute;

    public int VerificationAttemptsPerWindow { get; init; } = 10;

    public int PhotoTransferConcurrency { get; init; } = 4;

    public int WindowSeconds { get; init; } = 60;
}
