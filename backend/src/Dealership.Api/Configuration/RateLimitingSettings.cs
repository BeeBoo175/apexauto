namespace Dealership.Api.Configuration;

public sealed class RateLimitingSettings
{
    public const string SectionName = "RateLimiting";

    public int ApiRequestsPerWindow { get; init; } = ApiRateLimiting.RequestsPerMinute;

    public int BearerRequestsPerWindow { get; init; } = 600;

    public int UserReadBurst { get; init; } = 120;

    public int UserReadTokensPerSecond { get; init; } = 5;

    public int UserWritesPerWindow { get; init; } = 60;

    public int PictureReadsPerWindow { get; init; } = ApiRateLimiting.PictureReadsPerMinute;

    public int LoginAttemptsPerWindow { get; init; } = ApiRateLimiting.LoginAttemptsPerMinute;

    public int VerificationAttemptsPerWindow { get; init; } = 10;

    public int PhotoTransferConcurrency { get; init; } = 4;

    public int PhotoReadConcurrency { get; init; } = 8;

    public int PhotoReadQueueLimit { get; init; } = 96;

    public int PhotoReadQueueSeconds { get; init; } = 10;

    public int PictureDownloadConcurrency { get; init; } = 8;

    public int PictureDownloadQueueLimit { get; init; } = 48;

    public int PictureDownloadQueueSeconds { get; init; } = 2;

    public int WindowSeconds { get; init; } = 60;
}
