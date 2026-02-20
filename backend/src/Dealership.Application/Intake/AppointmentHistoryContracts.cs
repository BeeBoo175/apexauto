using Dealership.Application.Common;

namespace Dealership.Application.Intake;

public sealed record AppointmentHistoryQuery(DateTimeOffset? From, DateTimeOffset? Until, int Page, int PageSize)
{
    public static AppointmentHistoryQuery? Parse(string? from, string? to, int page, int pageSize)
    {
        var dates = ListDateRange.Parse(from, to);
        if (!Pagination.IsValid(page, pageSize) || dates is null)
        {
            return null;
        }

        return new AppointmentHistoryQuery(dates.From, dates.Until, page, pageSize);
    }
}

public sealed record AppointmentHistoryItem(Guid Id, Guid BranchId, string BranchName, string RegistrationPlate,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, DateTimeOffset CreatedAt, DateTimeOffset? CancelledAt,
    DateTimeOffset? NoShowAt = null, DateTimeOffset? CheckedInAt = null);

public sealed record AppointmentHistoryPage(IReadOnlyList<AppointmentHistoryItem> Items, int Total, int Page, int PageSize);

public interface IAppointmentHistoryStore
{
    Task<AppointmentHistoryPage?> ListAsync(Guid ownerId, AppointmentHistoryQuery query, CancellationToken cancellationToken);
}
