namespace Dealership.Infrastructure.Configuration;

public sealed class EmailDeliverySettings
{
    public const string SectionName = "Email";

    public string Mode { get; set; } = "Disabled";

    public string? SmtpHost { get; set; }

    public int SmtpPort { get; set; } = 25;

    public string? SenderEmail { get; set; }

    public string SenderDisplayName { get; set; } = "ApexAuto";

    public string? Username { get; set; }

    public string? AppPassword { get; set; }

    public bool EnableSsl { get; set; }
}
