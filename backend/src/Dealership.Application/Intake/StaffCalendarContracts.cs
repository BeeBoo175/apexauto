namespace Dealership.Application.Intake;

public sealed record StaffCalendarSnapshot(IReadOnlyList<StaffWorklistItem> Items, int Total,
    DateOnly From, DateOnly To);

public enum StaffCalendarStatus
{
    Succeeded,
    Forbidden,
    TooMany
}

public sealed record StaffCalendarResult(StaffCalendarStatus Status, StaffCalendarSnapshot? Snapshot = null);
