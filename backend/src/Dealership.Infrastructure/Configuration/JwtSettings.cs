using System.ComponentModel.DataAnnotations;

namespace Dealership.Infrastructure.Configuration;

public sealed class JwtSettings
{
    public const string SectionName = "JwtSettings";
    public const int MinimumSecretLength = 32;

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Range(1, 1440)]
    public int ExpiryMinutes { get; init; }

    [Required]
    [MinLength(MinimumSecretLength)]
    public string Secret { get; init; } = string.Empty;
}
