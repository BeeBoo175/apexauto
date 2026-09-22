using Dealership.Application.Accounts;
using Dealership.Domain.Accounts;
using Dealership.Domain.Common;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Accounts;

internal enum CustomerErasureOutcome
{
    // The erasure was applied now or an earlier identical command already applied it.
    Done,
    Conflict,
    // The actor's own checks refused it.
    ActorForbidden,
    SessionChanged,
    TargetNotOpenCustomer,
    Blocked,
    ProtectedSample
}

internal sealed record CustomerErasureRun(CustomerErasureOutcome Outcome, IReadOnlyList<ClosureBlocker>? Blockers = null,
    Guid ReceiptId = default, DateTimeOffset RecordedAt = default);

// What differs between a customer closing themselves and an Administrator erasing a customer: who may replay, the
// actor checks before the target row lock, and whether the target's own security stamp is the verified one.
internal sealed record CustomerErasureActor(Guid ActorId, string? Reason, Guid? ReplayActorId,
    Func<Task<CustomerErasureOutcome?>>? CheckBeforeTarget, string? TargetSecurityStamp);

// The locked sequence shared by every command that erases a customer. Lock order: picture owner advisory, closure
// advisory, actor row when separate, the customer's user row, vehicles, deposit requests and payments, profile.
internal static class CustomerErasureCommand
{
    public static async Task<CustomerErasureRun> RunAsync(DealershipDbContext database, Guid customerId,
        Guid operationId, CustomerErasureActor actor, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await ClosureLocks.LockOwnerAsync(database, customerId, cancellationToken);

        // A replay must match the kind and the target, and the actor when the caller names one.
        var receipt = await database.AccountEvents.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == operationId && item.TargetId == customerId && item.Kind == AccountEventKind.Erased &&
            (actor.ReplayActorId == null || item.ActorId == actor.ReplayActorId), cancellationToken);
        if (receipt is not null)
        {
            return new(CustomerErasureOutcome.Done, ReceiptId: receipt.Id, RecordedAt: receipt.RecordedAt);
        }

        // An operation id already recorded for anything else is a conflict, never a second row with the same key.
        if (await database.AccountEvents.AsNoTracking().AnyAsync(item => item.Id == operationId, cancellationToken))
        {
            return new(CustomerErasureOutcome.Conflict);
        }

        if (actor.CheckBeforeTarget is not null)
        {
            var refused = await actor.CheckBeforeTarget();
            if (refused is not null)
            {
                return new(refused.Value);
            }
        }

        var user = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {customerId} FOR UPDATE").AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (user is not { Role: AccountRoles.Customer, ClosedAt: null })
        {
            return new(CustomerErasureOutcome.TargetNotOpenCustomer);
        }

        if (actor.TargetSecurityStamp is not null && user.SecurityStamp != actor.TargetSecurityStamp)
        {
            return new(CustomerErasureOutcome.SessionChanged);
        }

        await ClosureLocks.LockObligationsAsync(database, customerId, cancellationToken);
        var now = await database.DatabaseNowAsync(cancellationToken);
        var blockers = await CustomerClosureBlockers.ReadAsync(database, customerId, now, cancellationToken);
        if (blockers.Count > 0)
        {
            return new(CustomerErasureOutcome.Blocked, blockers);
        }

        var profile = await database.CustomerProfiles.FromSqlInterpolated(
            $"SELECT * FROM \"CustomerProfiles\" WHERE \"UserId\" = {customerId} FOR UPDATE")
            .SingleAsync(cancellationToken);
        if (SampleAccountProtection.IsProtectedCustomer(profile.RegistrationKey))
        {
            return new(CustomerErasureOutcome.ProtectedSample);
        }

        var recorded = UtcTime.TruncateToMicroseconds(now);
        await CustomerErasure.ApplyAsync(database, customerId, profile, operationId, actor.ActorId, actor.Reason,
            recorded, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(CustomerErasureOutcome.Done, ReceiptId: operationId, RecordedAt: recorded);
    }
}
