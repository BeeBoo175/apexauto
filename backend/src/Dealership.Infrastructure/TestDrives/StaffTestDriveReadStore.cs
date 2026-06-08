using Dealership.Domain.Common;
using System.Data;
using Dealership.Application.Accounts;
using Dealership.Application.TestDrives;
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
            Booked = group.Count(row => row.Status == "Booked"),
            InProgress = group.Count(row => row.Status == "InProgress"),
            Completed = group.Count(row => row.Status == "Completed"),
            NotCompleted = group.Count(row => row.Status == "NotCompleted"),
            NoShow = group.Count(row => row.Status == "NoShow"),
            Cancelled = group.Count(row => row.Status == "Cancelled"),
            ToClaim = group.Count(row => row.Status == "Booked" && row.HandlerId == null),
            MyWork = group.Count(row => (row.Status == "Booked" || row.Status == "InProgress") && row.HandlerId == staffId),
            History = group.Count(row => row.Status != "Booked" && row.Status != "InProgress" && row.HandlerId == staffId)
        }).SingleOrDefaultAsync(cancellationToken);
        var counts = totals is null ? new StaffTestDriveCounts(0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
            : new(totals.All, totals.Booked, totals.InProgress, totals.Completed, totals.NotCompleted,
                totals.NoShow, totals.Cancelled, totals.ToClaim, totals.MyWork, totals.History);
        await transaction.CommitAsync(cancellationToken);
        return new(TestDriveReadStatus.Succeeded, new(actor, items.Select(StaffTestDriveRows.ToItem).ToArray(),
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
            rows = rows.Where(row => row.HandlerId == staffId || row.Status == "Booked" && row.HandlerId == null);
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
        var ids = new[] { start?.StaffId, outcome?.StaffId };
        var emails = await database.Users.AsNoTracking().Where(user => ids.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Email, cancellationToken);
        var detail = new StaffTestDriveDetail(actor, StaffTestDriveRows.ToItem(row), facts.Mobile, facts.Email ?? string.Empty,
            facts.CreatedAt, ownership, cancellation, start, outcome,
            start is null ? null : emails.GetValueOrDefault(start.StaffId),
            outcome is null ? null : emails.GetValueOrDefault(outcome.StaffId));
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

        var handlers = await database.Users.AsNoTracking().Where(user => user.IsActive && user.BranchId == actor.BranchId &&
                (user.Role == AccountRoles.SalesAgent || user.Role == AccountRoles.BranchManager))
            .OrderBy(user => user.Email).ThenBy(user => user.Id)
            .Select(user => new TestDriveHandler(user.Id, user.Email ?? string.Empty)).ToArrayAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(TestDriveReadStatus.Succeeded, new(actor, handlers));
    }
}
