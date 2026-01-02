namespace Dealership.Infrastructure.Intake;

public sealed class IntakePhotoSettings
{
    public double UnsubmittedLifetimeHours { get; set; }
    public double CancelledRetentionDays { get; set; }
    public bool CleanupEnabled { get; set; }
}
