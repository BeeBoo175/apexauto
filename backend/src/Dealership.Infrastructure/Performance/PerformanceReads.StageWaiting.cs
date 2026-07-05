using System.Data;
using Dealership.Application.Performance;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Performance;

public sealed partial class PerformanceReads
{
    // Six stages a vehicle passes through. Averages follow the first pass only, so reopened inspections and relisted
    // vehicles do not stretch them; the Now counts classify every vehicle by where it stands today.
    public async Task<PerformanceReadResult<PerformanceStageWaiting>> StageWaitingAsync(Guid actorId,
        PerformanceQuery query, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,
            cancellationToken);
        var scope = await ResolveScopeAsync(actorId, query, cancellationToken);
        if (scope is null)
        {
            return new(PerformanceReadStatus.Forbidden);
        }

        var branchId = scope.BranchId;
        var passes = new Dictionary<string, List<double>>
        {
            [PerformanceStages.AwaitingInspection] = await AwaitingInspectionPassesAsync(branchId, query, cancellationToken),
            [PerformanceStages.InInspection] = await InInspectionPassesAsync(branchId, query, cancellationToken),
            [PerformanceStages.InRepair] = await InRepairPassesAsync(branchId, query, cancellationToken),
            [PerformanceStages.AwaitingListing] = await AwaitingListingPassesAsync(branchId, query, cancellationToken),
            [PerformanceStages.Listed] = await ListedPassesAsync(branchId, query, cancellationToken),
            [PerformanceStages.SaleInProgress] = await SaleInProgressPassesAsync(branchId, query, cancellationToken)
        };
        var current = await CurrentStagesAsync(branchId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var stages = PerformanceStages.Ordered.Select(stage =>
        {
            // Evidence that runs backwards, such as a listing dated before its repair completed, is not a pass.
            var seconds = passes[stage].Where(value => value >= 0).ToList();
            return new PerformanceStage(stage, current.GetValueOrDefault(stage), seconds.Count,
                seconds.Count == 0 ? null : seconds.Average() / TimeSpan.SecondsPerDay);
        }).ToArray();
        return new(PerformanceReadStatus.Succeeded, new PerformanceStageWaiting(scope.Actor, stages));
    }

    private Task<List<double>> AwaitingInspectionPassesAsync(Guid? branchId, PerformanceQuery query,
        CancellationToken cancellationToken)
    {
        return (from checkIn in database.AppointmentCheckIns.AsNoTracking()
                join draft in database.InspectionDrafts.AsNoTracking() on checkIn.VehicleId equals draft.VehicleId
                join claim in database.InspectionAssignments.AsNoTracking() on draft.Id equals claim.InspectionId
                where claim.Version == 1 && claim.RecordedAt >= query.From && claim.RecordedAt < query.Until &&
                    (branchId == null || checkIn.BranchId == branchId)
                select (claim.RecordedAt - checkIn.CheckedInAt).TotalSeconds).ToListAsync(cancellationToken);
    }

    private Task<List<double>> InInspectionPassesAsync(Guid? branchId, PerformanceQuery query,
        CancellationToken cancellationToken)
    {
        return (from claim in database.InspectionAssignments.AsNoTracking()
                join signed in database.InspectionSignedRevisions.AsNoTracking() on claim.InspectionId equals signed.InspectionId
                where claim.Version == 1 && signed.Revision == 1 && signed.SignedAt >= query.From &&
                    signed.SignedAt < query.Until && (branchId == null || signed.BranchId == branchId)
                select (signed.SignedAt - claim.RecordedAt).TotalSeconds).ToListAsync(cancellationToken);
    }

    private Task<List<double>> InRepairPassesAsync(Guid? branchId, PerformanceQuery query,
        CancellationToken cancellationToken)
    {
        return (from request in database.InspectionRepairRequests.AsNoTracking()
                join completion in database.InspectionRepairCompletions.AsNoTracking() on request.InspectionId equals completion.InspectionId
                where request.Version == 1 && completion.RepairVersion == 1 && completion.CompletedAt >= query.From &&
                    completion.CompletedAt < query.Until && (branchId == null || completion.BranchId == branchId)
                select (completion.CompletedAt - request.RequestedAt).TotalSeconds).ToListAsync(cancellationToken);
    }

    // The wait for a listing starts when the vehicle was last ready: its first sign-off, or its first repair completion.
    private async Task<List<double>> AwaitingListingPassesAsync(Guid? branchId, PerformanceQuery query,
        CancellationToken cancellationToken)
    {
        var rows = await (from listing in FirstPublications(branchId)
                          join draft in database.InspectionDrafts.AsNoTracking() on listing.VehicleId equals draft.VehicleId
                          join signed in database.InspectionSignedRevisions.AsNoTracking() on draft.Id equals signed.InspectionId
                          where signed.Revision == 1 && listing.ListedAt >= query.From && listing.ListedAt < query.Until
                          select new
                          {
                              listing.ListedAt,
                              signed.SignedAt,
                              Repaired = database.InspectionRepairCompletions
                                  .Where(item => item.InspectionId == draft.Id && item.RepairVersion == 1)
                                  .Select(item => (DateTimeOffset?)item.CompletedAt).FirstOrDefault()
                          }).ToListAsync(cancellationToken);
        return rows.Select(row =>
        {
            var ready = row.Repaired is { } repaired && repaired > row.SignedAt ? repaired : row.SignedAt;
            return (row.ListedAt - ready).TotalSeconds;
        }).ToList();
    }

    private Task<List<double>> ListedPassesAsync(Guid? branchId, PerformanceQuery query,
        CancellationToken cancellationToken)
    {
        var firstReservations = database.Reservations.AsNoTracking()
            .GroupBy(reservation => reservation.VehicleId)
            .Select(group => new { VehicleId = group.Key, ReservedAt = group.Min(reservation => reservation.CreatedAt) });
        return (from listing in FirstPublications(branchId)
                join reserved in firstReservations on listing.VehicleId equals reserved.VehicleId
                where reserved.ReservedAt >= query.From && reserved.ReservedAt < query.Until &&
                    reserved.ReservedAt >= listing.ListedAt
                select (reserved.ReservedAt - listing.ListedAt).TotalSeconds).ToListAsync(cancellationToken);
    }

    // A sale shares its reservation's identity, so the pass runs from that hold to the delivery.
    private Task<List<double>> SaleInProgressPassesAsync(Guid? branchId, PerformanceQuery query,
        CancellationToken cancellationToken)
    {
        return (from order in SalesInScope(branchId)
                join reservation in database.Reservations.AsNoTracking() on order.Id equals reservation.Id
                where order.DeliveredAt >= query.From && order.DeliveredAt < query.Until
                select (order.DeliveredAt!.Value - reservation.CreatedAt).TotalSeconds).ToListAsync(cancellationToken);
    }

    private IQueryable<FirstPublication> FirstPublications(Guid? branchId)
    {
        return database.ListingPublications.AsNoTracking()
            .Where(publication => branchId == null || publication.BranchId == branchId)
            .GroupBy(publication => publication.VehicleId)
            .Select(group => new FirstPublication
            {
                VehicleId = group.Key,
                ListedAt = group.Min(publication => publication.RecordedAt)
            });
    }

    // Each vehicle that is not yet delivered stands in exactly one stage; a later stage wins when evidence overlaps.
    private async Task<Dictionary<string, int>> CurrentStagesAsync(Guid? branchId, CancellationToken cancellationToken)
    {
        var vehicles = await database.Vehicles.AsNoTracking()
            .Where(vehicle => (branchId == null || vehicle.BranchId == branchId) &&
                vehicle.Status != VehicleStatus.Delivered && vehicle.Status != VehicleStatus.Rejected)
            .Select(vehicle => new
            {
                HasDraft = database.InspectionDrafts.Any(draft => draft.VehicleId == vehicle.Id),
                Signed = database.InspectionDrafts.Any(draft => draft.VehicleId == vehicle.Id && draft.IsSigned),
                RepairOpen = database.InspectionDrafts.Any(draft => draft.VehicleId == vehicle.Id && draft.IsSigned &&
                    draft.RepairVersion > 0 && !database.InspectionRepairCompletions.Any(completion =>
                        completion.InspectionId == draft.Id && completion.RepairVersion == draft.RepairVersion)),
                Listed = database.ListingPublications.Any(publication => publication.VehicleId == vehicle.Id &&
                    !database.ListingWithdrawals.Any(withdrawal => withdrawal.PublicationId == publication.Id)),
                InSale = database.Reservations.Any(reservation => reservation.VehicleId == vehicle.Id && reservation.IsActive) ||
                    database.SalesOrders.Any(order => order.VehicleId == vehicle.Id &&
                        order.Status != SalesOrderStatus.Expired && order.Status != SalesOrderStatus.Cancelled &&
                        order.Status != SalesOrderStatus.Delivered)
            })
            .ToListAsync(cancellationToken);
        return vehicles.GroupBy(vehicle => StageOf(vehicle.HasDraft, vehicle.Signed, vehicle.RepairOpen, vehicle.Listed,
                vehicle.InSale))
            .ToDictionary(group => group.Key, group => group.Count());
    }

    private static string StageOf(bool hasDraft, bool signed, bool repairOpen, bool listed, bool inSale)
    {
        if (inSale)
        {
            return PerformanceStages.SaleInProgress;
        }

        if (listed)
        {
            return PerformanceStages.Listed;
        }

        if (repairOpen)
        {
            return PerformanceStages.InRepair;
        }

        if (signed)
        {
            return PerformanceStages.AwaitingListing;
        }

        return hasDraft ? PerformanceStages.InInspection : PerformanceStages.AwaitingInspection;
    }

    private sealed class FirstPublication
    {
        public Guid VehicleId { get; init; }

        public DateTimeOffset ListedAt { get; init; }
    }
}
