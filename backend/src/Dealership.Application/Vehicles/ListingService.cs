using Dealership.Application.Accounts;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed class ListingService(IListingStore listings)
{
    public async Task<ListingResult> SaveAsync(Guid actorId, Guid vehicleId, ListingDraftRequest request,
        CancellationToken cancellationToken)
    {
        if (!ValidIds(actorId, vehicleId, request.OperationId) || request.ExpectedVersion is < 0 or long.MaxValue ||
            request.PhotoIds is null || request.PhotoIds.Contains(Guid.Empty) ||
            request.PhotoIds.Distinct().Count() != request.PhotoIds.Count)
        {
            return new(ListingStatus.Invalid);
        }

        await using var transaction = await listings.LockAsync(actorId, vehicleId, request.OperationId, cancellationToken, request.PhotoIds);
        var context = transaction.Context;
        var denied = CheckAccess(context);
        if (denied is not null)
        {
            return new(denied.Value);
        }

        if (context.ExistingPublication is not null || context.ExistingWithdrawal is not null)
        {
            return new(ListingStatus.Conflict);
        }

        ListingRevision revision;
        try
        {
            revision = new ListingRevision(request.OperationId, vehicleId, context.Vehicle!.BranchId, actorId,
                request.ExpectedVersion + 1, request.VehiclePrice, request.RoadTax, request.Transmission,
                request.Fuel, request.Description!, context.Now, request.PhotoIds);
        }
        catch (ArgumentException)
        {
            return new(ListingStatus.Invalid);
        }
        catch (OverflowException)
        {
            return new(ListingStatus.Invalid);
        }

        var existing = context.ExistingDraft;
        if (existing is not null)
        {
            var matches = existing.VehicleId == vehicleId && existing.ActorId == actorId &&
                existing.Version == revision.Version && existing.VehiclePrice == revision.VehiclePrice &&
                existing.RoadTax == revision.RoadTax && existing.Transmission == revision.Transmission &&
                existing.Fuel == revision.Fuel && existing.Description == revision.Description &&
                existing.PhotoIds.SequenceEqual(request.PhotoIds);
            return matches ? new(ListingStatus.Succeeded, Draft: existing) : new(ListingStatus.Conflict);
        }

        if ((context.LatestRevision?.Version ?? 0) != request.ExpectedVersion ||
            request.PhotoIds.Any(id => !context.AvailablePhotoIds.Contains(id)))
        {
            return new(ListingStatus.Conflict);
        }

        var status = await transaction.SaveAsync(revision, request.PhotoIds, cancellationToken);
        return new(status, Draft: status == ListingStatus.Succeeded ? revision : null);
    }

    public async Task<ListingResult> PublishAsync(Guid actorId, Guid vehicleId, ListingPublishRequest request,
        CancellationToken cancellationToken)
    {
        if (!ValidIds(actorId, vehicleId, request.OperationId) || request.RevisionId == Guid.Empty || request.SignedRevisionId == Guid.Empty)
        {
            return new(ListingStatus.Invalid);
        }

        await using var transaction = await listings.LockAsync(actorId, vehicleId, request.OperationId, cancellationToken);
        var context = transaction.Context;
        var denied = CheckAccess(context);
        if (denied is not null)
        {
            return new(denied.Value);
        }

        if (context.ExistingDraft is not null || context.ExistingWithdrawal is not null)
        {
            return new(ListingStatus.Conflict);
        }

        var existing = context.ExistingPublication;
        if (existing is not null)
        {
            return existing.ActorId == actorId && existing.VehicleId == vehicleId &&
                existing.RevisionId == request.RevisionId && existing.SignedRevisionId == request.SignedRevisionId
                ? new(ListingStatus.Succeeded, Publication: existing) : new(ListingStatus.Conflict);
        }

        var revision = context.LatestRevision;
        if (revision?.Id != request.RevisionId || context.Vehicle!.Status != VehicleStatus.Available ||
            context.SignedRevisionId != request.SignedRevisionId || context.LatestPublication?.RevisionId == request.RevisionId)
        {
            return new(ListingStatus.Conflict);
        }

        var publication = new ListingPublication(request.OperationId, revision!, request.SignedRevisionId, actorId, context.Now);
        var status = await transaction.PublishAsync(publication, cancellationToken);
        return new(status, Publication: status == ListingStatus.Succeeded ? publication : null);
    }

    public async Task<ListingResult> WithdrawAsync(Guid actorId, Guid vehicleId, ListingWithdrawRequest request,
        CancellationToken cancellationToken)
    {
        if (!ValidIds(actorId, vehicleId, request.OperationId) || request.PublicationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 1000 || request.Reason.Any(char.IsControl))
        {
            return new(ListingStatus.Invalid);
        }

        await using var transaction = await listings.LockAsync(actorId, vehicleId, request.OperationId, cancellationToken);
        var context = transaction.Context;
        var denied = CheckAccess(context);
        if (denied is not null)
        {
            return new(denied.Value);
        }

        if (context.ExistingDraft is not null || context.ExistingPublication is not null)
        {
            return new(ListingStatus.Conflict);
        }

        var existing = context.ExistingWithdrawal;
        if (existing is not null)
        {
            return existing.ActorId == actorId && existing.VehicleId == vehicleId &&
                existing.PublicationId == request.PublicationId && existing.Reason == request.Reason.Trim()
                ? new(ListingStatus.Succeeded, Withdrawal: existing) : new(ListingStatus.Conflict);
        }

        if (context.LatestPublication?.Id != request.PublicationId || context.IsWithdrawn)
        {
            return new(ListingStatus.Conflict);
        }

        var withdrawal = new ListingWithdrawal(request.OperationId, context.LatestPublication!, actorId, request.Reason, context.Now);
        var status = await transaction.WithdrawAsync(withdrawal, cancellationToken);
        return new(status, Withdrawal: status == ListingStatus.Succeeded ? withdrawal : null);
    }

    private static bool ValidIds(Guid actorId, Guid vehicleId, Guid operationId) =>
        actorId != Guid.Empty && vehicleId != Guid.Empty && operationId != Guid.Empty;

    private static ListingStatus? CheckAccess(ListingContext context)
    {
        var actor = context.Actor;
        if (actor is not { IsActive: true } ||
            actor.Role is not (AccountRoles.BranchManager or AccountRoles.GeneralManager) ||
            actor.Role == AccountRoles.BranchManager && actor.BranchId is null)
        {
            return ListingStatus.Forbidden;
        }

        if (context.Vehicle is null || !context.BranchActive ||
            actor.Role == AccountRoles.BranchManager && actor.BranchId != context.Vehicle.BranchId)
        {
            return ListingStatus.NotFound;
        }

        return null;
    }
}
