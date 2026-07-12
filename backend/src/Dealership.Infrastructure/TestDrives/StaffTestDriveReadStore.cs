using Dealership.Domain.TestDrives;
using Dealership.Domain.Common;
using System.Data;
using Dealership.Application.Accounts;
using Dealership.Application.Common;
using Dealership.Application.TestDrives;
using Dealership.Infrastructure.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.TestDrives;

public sealed class StaffTestDriveReadStore(DealershipDbContext database) : IStaffTestDriveReadStore
{
    public async Task<StaffTestDriveReadResult<StaffTestDrivePage>> ListAsync(Guid staffId,
        StaffTestDriveQuery query, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var actor = await StaffTestDriveRows.ActorAsync(database, staffId, cancellationToken);
        if (actor is null || actor.BranchId is not null && query.BranchId is not null && actor.BranchId != query.BranchId ||
            actor.Role != AccountRoles.SalesAgent && query.Queue.Length > 0 ||
            actor.Role == AccountRoles.SalesAgent && query.Queue.Length == 0)
        {
            return new(TestDriveReadStatus.Forbidden);
        }

        var now = await database.DatabaseNowAsync(cancellationToken);
        var scope = StaffTestDriveRows.Create(database, actor.BranchId ?? query.BranchId, AppointmentDay.StartUtc(now));
        var records = StaffTestDriveRows.Filter(StaffTestDriveRows.Queue(scope, query.Queue, staffId), query);
        var total = await records.CountAsync(cancellationToken);
        var items = await StaffTestDriveRows.Order(records, query).ThenBy(row => row.Id)
            .ReadPageAsync(query.Page, query.PageSize, total, cancellationToken);
        var totals = await scope.GroupBy(row => 1).Select(group => new
        {
            All = group.Count(),
            Booked = group.Count(row => row.Status == TestDriveStatus.Booked),
            InProgress = group.Count(row => row.Status == TestDriveStatus.InProgress),
            Completed = group.Count(row => row.Status == TestDriveStatus.Completed),
            NotCompleted = group.Count(row => row.Status == TestDriveStatus.NotCompleted),
            NoShow = group.Count(row => row.Status == TestDriveStatus.NoShow),
            Cancelled = group.Count(row => row.Status == TestDriveStatus.Cancelled),
            ToClaim = group.Count(row => row.Status == TestDriveStatus.Booked && row.HandlerId == null),
            MyWork = group.Count(row => (row.Status == TestDriveStatus.Booked || row.Status == TestDriveStatus.InProgress) && row.HandlerId == staffId),
            History = group.Count(row => row.Status != TestDriveStatus.Booked && row.Status != TestDriveStatus.InProgress && row.HandlerId == staffId)
        }).SingleOrDefaultAsync(cancellationToken);
        var counts = totals is null ? new StaffTestDriveCounts(0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
            : new(totals.All, totals.Booked, totals.InProgress, totals.Completed, totals.NotCompleted,
                totals.NoShow, totals.Cancelled, totals.ToClaim, totals.MyWork, totals.History);
        await transaction.CommitAsync(cancellationToken);
        return new(TestDriveReadStatus.Succeeded, new(actor, [.. items.Select(StaffTestDriveRows.ToItem)],
            total, query.Page, query.PageSize, counts));
    }

    public async Task<StaffTestDriveReadResult<StaffTestDriveDetail>> GetAsync(Guid staffId, Guid bookingId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var actor = await StaffTestDriveRows.ActorAsync(database, staffId, cancellationToken);
        if (actor is null)
        {
            return new(TestDriveReadStatus.Forbidden);
        }

        var now = await database.DatabaseNowAsync(cancellationToken);
        var rows = StaffTestDriveRows.Create(database, actor.BranchId, AppointmentDay.StartUtc(now));
        if (actor.Role == AccountRoles.SalesAgent)
        {
            rows = rows.Where(row => row.HandlerId == staffId || row.Status == TestDriveStatus.Booked && row.HandlerId == null);
        }

        var row = await rows.SingleOrDefaultAsync(item => item.Id == bookingId, cancellationToken);
        if (row is null)
        {
            return new(TestDriveReadStatus.NotFound);
        }

        var facts = await (from booking in database.TestDriveBookings.AsNoTracking()
                           join customer in database.CustomerProfiles on booking.CustomerId equals customer.UserId
                           join user in database.Users on booking.CustomerId equals user.Id
                           where booking.Id == bookingId
                           select new { booking.CreatedAt, customer.Mobile, user.Email }).SingleAsync(cancellationToken);
        var ownership = await database.TestDriveAssignments.AsNoTracking().Where(item => item.BookingId == bookingId)
            .OrderByDescending(item => item.Version)
            .Select(item => new TestDriveOwnership(item.Id, item.BookingId, item.Version, item.HandlerId,
                item.ActorId, item.Kind, item.RecordedAt, item.Reason)).FirstOrDefaultAsync(cancellationToken);
        var cancellation = await database.TestDriveCancellations.AsNoTracking()
            .Where(item => item.BookingId == bookingId)
            .Select(item => new TestDriveCancellationView(item.Id, item.BookingId, item.Kind, item.StaffId,
                item.RecordedAt, item.Reason)).SingleOrDefaultAsync(cancellationToken);
        var start = await database.TestDriveStarts.AsNoTracking()
            .Where(item => item.BookingId == bookingId)
            .Select(item => new TestDriveStartView(item.Id, item.BookingId, item.AssignmentId, item.StaffId,
                item.CustomerPresent, item.DrivingLicenceChecked, item.StartedAt)).SingleOrDefaultAsync(cancellationToken);
        var outcome = await database.TestDriveOutcomes.AsNoTracking()
            .Where(item => item.BookingId == bookingId)
            .Select(item => new TestDriveOutcomeView(item.Id, item.BookingId, item.AssignmentId, item.StaffId,
                item.StartId, item.Kind, item.CustomerPresent, item.Reason, item.Notes, item.EndedAt))
            .SingleOrDefaultAsync(cancellationToken);
        var detail = new StaffTestDriveDetail(actor, StaffTestDriveRows.ToItem(row), facts.Mobile, facts.Email ?? string.Empty,
            facts.CreatedAt, ownership, cancellation, start, outcome);
        await transaction.CommitAsync(cancellationToken);
        return new(TestDriveReadStatus.Succeeded, detail);
    }

    public async Task<StaffTestDriveReadResult<TestDriveHandlers>> HandlersAsync(Guid staffId, Guid bookingId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var actor = await StaffTestDriveRows.ActorAsync(database, staffId, cancellationToken);
        if (actor?.Role != AccountRoles.BranchManager)
        {
            return new(TestDriveReadStatus.Forbidden);
        }

        if (!await database.TestDriveBookings.AnyAsync(item => item.Id == bookingId && item.BranchId == actor.BranchId, cancellationToken))
        {
            return new(TestDriveReadStatus.NotFound);
        }

        var handlers = await StaffHandlerRows.ReadAsync(database, actor.BranchId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(TestDriveReadStatus.Succeeded, new(actor, handlers));
    }

    // Every retained step of one booking, newest first: the booking, each ownership change, the start, the outcome
    // and the cancellation. A booking has few steps, so they are merged and paged in memory.
    public async Task<StaffTestDriveReadResult<TestDriveEventPage>> HistoryAsync(Guid staffId, Guid bookingId,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        if (!Pagination.IsValid(page, pageSize))
        {
            return new(TestDriveReadStatus.Invalid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var actor = await StaffTestDriveRows.ActorAsync(database, staffId, cancellationToken);
        if (actor is null)
        {
            return new(TestDriveReadStatus.Forbidden);
        }

        var now = await database.DatabaseNowAsync(cancellationToken);
        var rows = StaffTestDriveRows.Create(database, actor.BranchId, AppointmentDay.StartUtc(now));
        if (actor.Role == AccountRoles.SalesAgent)
        {
            rows = rows.Where(row => row.HandlerId == staffId || row.Status == TestDriveStatus.Booked && row.HandlerId == null);
        }

        if (!await rows.AnyAsync(item => item.Id == bookingId, cancellationToken))
        {
            return new(TestDriveReadStatus.NotFound);
        }

        var booking = await database.TestDriveBookings.AsNoTracking().SingleAsync(item => item.Id == bookingId, cancellationToken);
        var assignments = await database.TestDriveAssignments.AsNoTracking().Where(item => item.BookingId == bookingId)
            .ToListAsync(cancellationToken);
        var start = await database.TestDriveStarts.AsNoTracking().SingleOrDefaultAsync(item => item.BookingId == bookingId, cancellationToken);
        var outcome = await database.TestDriveOutcomes.AsNoTracking().SingleOrDefaultAsync(item => item.BookingId == bookingId, cancellationToken);
        var cancellation = await database.TestDriveCancellations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.BookingId == bookingId, cancellationToken);
        var names = await AccountNames.ReadAsync(database,
        [
            booking.CustomerId, start?.StaffId, outcome?.StaffId, cancellation?.StaffId,
            .. assignments.SelectMany(item => new Guid?[] { item.ActorId, item.HandlerId })
        ], cancellationToken);

        var items = new List<TestDriveEvent>
        {
            new(booking.Id, "Booked", booking.CreatedAt, NameOf(booking.CustomerId), booking.CustomerId, null, null, null, null)
        };
        items.AddRange(assignments.Select(item => new TestDriveEvent(item.Id, item.Kind, item.RecordedAt,
            NameOf(item.ActorId), item.ActorId, item.HandlerId is { } handler ? NameOf(handler) : null, item.HandlerId,
            item.Reason, null)));
        if (start is not null)
        {
            items.Add(new(start.Id, "Started", start.StartedAt, NameOf(start.StaffId), start.StaffId, null, null, null,
                $"{(start.CustomerPresent ? "Customer present" : "Customer absent")} · " +
                (start.DrivingLicenceChecked ? "Driving licence checked" : "Driving licence not checked")));
        }

        if (outcome is not null)
        {
            items.Add(new(outcome.Id, outcome.Kind, outcome.EndedAt, NameOf(outcome.StaffId), outcome.StaffId, null, null,
                outcome.Reason, outcome.Notes));
        }

        if (cancellation is not null)
        {
            Guid? cancelledBy = cancellation.StaffId;
            if (cancelledBy is null && cancellation.Kind == TestDriveCancellationKind.CustomerCancelled)
            {
                cancelledBy = cancellation.CustomerId;
            }

            items.Add(new(cancellation.Id, cancellation.Kind, cancellation.RecordedAt,
                cancelledBy is { } who ? NameOf(who) : "System", cancelledBy, null, null, cancellation.Reason, null));
        }

        var ordered = items.OrderByDescending(item => item.RecordedAt).ThenByDescending(item => item.Kind != "Booked")
            .ThenBy(item => item.Id).ToList();
        var pageItems = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        await transaction.CommitAsync(cancellationToken);
        return new(TestDriveReadStatus.Succeeded, new TestDriveEventPage(actor, bookingId, pageItems, ordered.Count, page, pageSize));

        string NameOf(Guid id) => names.GetValueOrDefault(id, string.Empty);
    }
}
