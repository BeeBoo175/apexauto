namespace Dealership.Domain.TestDrives;

public static class TestDriveStatus
{
    public const string Booked = "Booked";
    public const string InProgress = "InProgress";
    public const string Completed = TestDriveOutcomeKind.Completed;
    public const string NotCompleted = TestDriveOutcomeKind.NotCompleted;
    public const string NoShow = "NoShow";
    public const string Cancelled = "Cancelled";
}
