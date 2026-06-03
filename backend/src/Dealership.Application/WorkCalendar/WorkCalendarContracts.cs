using Dealership.Application.Accounts;
using Dealership.Application.Common;

namespace Dealership.Application.WorkCalendar;

public sealed record WorkCalendarScope(Guid? BranchId, string Employee = "")
{
    public bool IsValid => BranchId != Guid.Empty && Employee.Length <= 160;
}

public sealed record WorkCalendarQuery(ListDateRange Dates, WorkCalendarScope Scope)
{
    public const int MaximumEvents = 1000;

    public bool IsValid => Scope.IsValid && Dates is { From: not null, Until: not null } &&
        Dates.Until > Dates.From && Dates.Until - Dates.From <= TimeSpan.FromDays(42);

    public static WorkCalendarQuery? Parse(string? from, string? to, Guid? branchId, string? employee)
    {
        var dates = ListDateRange.Parse(from, to);
        var scope = new WorkCalendarScope(branchId, employee?.Trim() ?? string.Empty);
        return dates is { From: not null, Until: not null } &&
            dates.Until - dates.From <= TimeSpan.FromDays(42) && scope.IsValid
            ? new(dates, scope) : null;
    }
}

public sealed record UnscheduledWorkQuery(WorkCalendarScope Scope, int Page, int PageSize)
{
    public bool IsValid => Scope.IsValid && Pagination.IsValid(Page, PageSize);

    public static UnscheduledWorkQuery? Parse(Guid? branchId, string? employee, int page, int pageSize)
    {
        var scope = new WorkCalendarScope(branchId, employee?.Trim() ?? string.Empty);
        return scope.IsValid && Pagination.IsValid(page, pageSize) ? new(scope, page, pageSize) : null;
    }
}

public sealed record WorkCalendarEvent(Guid Id, string Kind, Guid BranchId, string BranchName,
    string RegistrationPlate, string Vehicle, string Status, Guid? HandlerId, string? HandlerEmail,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt);

public sealed record WorkCalendarSnapshot(CurrentActor Actor, IReadOnlyList<WorkCalendarEvent> Items,
    int Total, DateOnly From, DateOnly To, Guid BranchId, string Employee);

public sealed record UnscheduledWorkItem(Guid Id, string Kind, Guid BranchId, string BranchName,
    string RegistrationPlate, string Vehicle, string Status, Guid HandlerId, string HandlerEmail,
    Guid? InspectionId);

public sealed record UnscheduledWorkPage(CurrentActor Actor, IReadOnlyList<UnscheduledWorkItem> Items,
    int Total, int Page, int PageSize, Guid BranchId, string Employee);

public enum WorkCalendarReadStatus
{
    Succeeded,
    Forbidden,
    BranchRequired,
    Invalid,
    TooMany
}

public sealed record WorkCalendarReadResult<T>(WorkCalendarReadStatus Status, T? Value = default);

public interface IWorkCalendarStore
{
    Task<WorkCalendarReadResult<WorkCalendarSnapshot>> ScheduledAsync(Guid actorId,
        WorkCalendarQuery query, CancellationToken cancellationToken);

    Task<WorkCalendarReadResult<UnscheduledWorkPage>> UnscheduledAsync(Guid actorId,
        UnscheduledWorkQuery query, CancellationToken cancellationToken);
}
