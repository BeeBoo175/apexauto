using System.Data;
using Dealership.Application.Accounts;
using Dealership.Application.Performance;
using Dealership.Domain.Sales;
using Dealership.Domain.TestDrives;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MilestoneQuery = Dealership.Application.Performance.PerformanceMilestoneQuery;

namespace Dealership.Infrastructure.Performance;

public sealed class PerformanceMilestoneQuery(DealershipDbContext database) : IPerformanceMilestoneQuery
{
    // One employee's completed work in the period, newest first. Rows keep the branch recorded at the event, and a
    // manager only sees events of their own branch, so every row they see is a record they can open.
    public async Task<PerformanceReadResult<PerformanceMilestonePage>> MilestonesAsync(Guid actorId,
        MilestoneQuery query, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,
            cancellationToken);
        var period = query.Period;
        var scope = await PerformanceScopes.ResolveAsync(database, actorId, period, cancellationToken);
        if (scope is null)
        {
            return new(PerformanceReadStatus.Forbidden);
        }

        var personId = query.PersonId;
        var name = await PersonRows.Staff(database).Where(person => person.UserId == personId)
            .Select(person => person.Name).SingleOrDefaultAsync(cancellationToken);
        if (name is null)
        {
            return new(PerformanceReadStatus.NotFound);
        }

        var branchId = scope.BranchId;
        var found = new List<Milestone>();
        found.AddRange(await CheckInMilestonesAsync(personId, branchId, period, cancellationToken));
        found.AddRange(await DriveMilestonesAsync(personId, branchId, period, cancellationToken));
        found.AddRange(await SaleMilestonesAsync(personId, branchId, period, cancellationToken));
        found.AddRange(await WorkshopMilestonesAsync(personId, branchId, period, cancellationToken));
        var branches = await database.Branches.AsNoTracking().ToDictionaryAsync(branch => branch.Id,
            branch => branch.Name, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var ordered = found.OrderByDescending(item => item.At).ThenBy(item => item.RecordId).ThenBy(item => item.Kind)
            .ToArray();
        var items = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(item => new PerformanceMilestone(item.Kind, item.At, item.BranchId,
                branches.GetValueOrDefault(item.BranchId, string.Empty), item.Reference, item.RecordKind,
                item.RecordId, item.AgreedTotal, HasAccess(scope.Actor, item.BranchId)))
            .ToArray();
        return new(PerformanceReadStatus.Succeeded, new PerformanceMilestonePage(scope.Actor, personId, name, items,
            ordered.Length, query.Page, query.PageSize));
    }

    private static bool HasAccess(CurrentActor viewer, Guid eventBranchId)
    {
        return viewer.Role == AccountRoles.GeneralManager || viewer.BranchId == eventBranchId;
    }

    private async Task<List<Milestone>> CheckInMilestonesAsync(Guid personId, Guid? branchId, PerformanceQuery query,
        CancellationToken cancellationToken)
    {
        var rows = await (from checkIn in database.AppointmentCheckIns.AsNoTracking()
                          join appointment in database.IntakeAppointments.AsNoTracking() on checkIn.AppointmentId equals appointment.Id
                          where checkIn.StaffId == personId && checkIn.CheckedInAt >= query.From &&
                              checkIn.CheckedInAt < query.Until && (branchId == null || checkIn.BranchId == branchId)
                          select new
                          {
                              checkIn.AppointmentId,
                              checkIn.BranchId,
                              checkIn.CheckedInAt,
                              Corrected = checkIn.CorrectionRecordedAt != null,
                              appointment.RegistrationPlate
                          }).ToListAsync(cancellationToken);
        return [.. rows.Select(row => new Milestone(
            row.Corrected ? PerformanceMilestoneKinds.NoShowCorrection : PerformanceMilestoneKinds.CheckIn,
            row.CheckedInAt, row.BranchId, row.RegistrationPlate, "Appointment", row.AppointmentId, null))];
    }

    private async Task<List<Milestone>> DriveMilestonesAsync(Guid personId, Guid? branchId, PerformanceQuery query,
        CancellationToken cancellationToken)
    {
        var rows = await (from outcome in database.TestDriveOutcomes.AsNoTracking()
                          join booking in database.TestDriveBookings.AsNoTracking() on outcome.BookingId equals booking.Id
                          join vehicle in database.Vehicles.AsNoTracking() on booking.VehicleId equals vehicle.Id
                          where outcome.StaffId == personId && outcome.EndedAt >= query.From &&
                              outcome.EndedAt < query.Until && (branchId == null || outcome.BranchId == branchId)
                          select new { outcome.BookingId, outcome.BranchId, outcome.EndedAt, outcome.Kind, vehicle.RegistrationPlate })
            .ToListAsync(cancellationToken);
        return [.. rows.Select(row => new Milestone(
            row.Kind == TestDriveOutcomeKind.Completed
                ? PerformanceMilestoneKinds.DriveCompleted
                : PerformanceMilestoneKinds.DriveNotCompleted,
            row.EndedAt, row.BranchId, row.RegistrationPlate, "TestDrive", row.BookingId, null))];
    }

    private async Task<List<Milestone>> SaleMilestonesAsync(Guid personId, Guid? branchId, PerformanceQuery query,
        CancellationToken cancellationToken)
    {
        var rows = await (from entry in database.SalesOrderEvents.AsNoTracking()
                          join order in database.SalesOrders.AsNoTracking().Select(SalesOrderValue.Projection)
                              on entry.SalesOrderId equals order.Id
                          where entry.ActorId == personId &&
                              (entry.Kind == SalesOrderEventKind.Settled || entry.Kind == SalesOrderEventKind.Delivered) &&
                              entry.RecordedAt >= query.From && entry.RecordedAt < query.Until &&
                              (branchId == null || order.BranchId == branchId)
                          select new
                          {
                              OrderId = order.Id,
                              order.BranchId,
                              entry.Kind,
                              entry.RecordedAt,
                              order.OrderNumber,
                              Total = order.Subtotal
                          }).ToListAsync(cancellationToken);
        return [.. rows.Select(row => row.Kind == SalesOrderEventKind.Delivered
            ? new Milestone(PerformanceMilestoneKinds.SaleDelivered, row.RecordedAt, row.BranchId,
                row.OrderNumber ?? string.Empty, "Sale", row.OrderId, row.Total)
            : new Milestone(PerformanceMilestoneKinds.SaleSettled, row.RecordedAt, row.BranchId,
                row.OrderNumber ?? string.Empty, "Sale", row.OrderId, null))];
    }

    private async Task<List<Milestone>> WorkshopMilestonesAsync(Guid personId, Guid? branchId, PerformanceQuery query,
        CancellationToken cancellationToken)
    {
        var signed = await (from item in database.InspectionSignedRevisions.AsNoTracking()
                            join draft in database.InspectionDrafts.AsNoTracking() on item.InspectionId equals draft.Id
                            join vehicle in database.Vehicles.AsNoTracking() on draft.VehicleId equals vehicle.Id
                            where item.TechnicianId == personId && item.SignedAt >= query.From &&
                                item.SignedAt < query.Until && (branchId == null || item.BranchId == branchId)
                            select new { item.InspectionId, item.BranchId, item.Revision, item.SignedAt, vehicle.RegistrationPlate })
            .ToListAsync(cancellationToken);
        var repairs = await (from item in database.InspectionRepairCompletions.AsNoTracking()
                             join draft in database.InspectionDrafts.AsNoTracking() on item.InspectionId equals draft.Id
                             join vehicle in database.Vehicles.AsNoTracking() on draft.VehicleId equals vehicle.Id
                             where item.ActorId == personId && item.CompletedAt >= query.From &&
                                 item.CompletedAt < query.Until && (branchId == null || item.BranchId == branchId)
                             select new { item.InspectionId, item.BranchId, item.CompletedAt, vehicle.RegistrationPlate })
            .ToListAsync(cancellationToken);
        return [.. signed.Select(row => new Milestone(
                row.Revision == 1 ? PerformanceMilestoneKinds.InspectionSigned : PerformanceMilestoneKinds.Reinspection,
                row.SignedAt, row.BranchId, row.RegistrationPlate, "Inspection", row.InspectionId, null))
            .Concat(repairs.Select(row => new Milestone(PerformanceMilestoneKinds.RepairCompleted, row.CompletedAt,
                row.BranchId, row.RegistrationPlate, "Inspection", row.InspectionId, null)))
            ];
    }

    private sealed record Milestone(string Kind, DateTimeOffset At, Guid BranchId, string Reference, string RecordKind,
        Guid RecordId, decimal? AgreedTotal);
}
