using Dealership.Application.Accounts;
using Dealership.Domain.Common;
using Dealership.Domain.Staff;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.Infrastructure.Identity;

public sealed class StaffInvitationStore(DealershipDbContext database, UserManager<AccountUser> users,
    IPasswordHasher<AccountUser> passwords, StaffInvitationToken tokens, TimeProvider clock,
    MailDeliverySignal mail) : IStaffInvitationStore
{
    private const string PendingEmailIndex = "StaffInvitationPendingEmailIndex";
    private const string UsernameIndex = "AccountUsernameIndex";
    private const string EmailIndex = "EmailIndex";

    public async Task<StaffInvitationResult> InviteAsync(Guid administratorId, ValidatedStaffInvitation invitation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            if (!await IsAdministratorAsync(administratorId, cancellationToken))
            {
                return Result(StaffInvitationStatus.Forbidden);
            }

            var existing = await database.StaffInvitations.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == invitation.Id, cancellationToken);

            if (existing is not null)
            {
                return Result(existing.InvitedById == administratorId && Matches(existing, invitation)
                    ? StaffInvitationStatus.Succeeded : StaffInvitationStatus.Invalid);
            }

            if (invitation.BranchId is { } branchId && !await database.Branches.AnyAsync(
                    branch => branch.Id == branchId && branch.IsActive, cancellationToken))
            {
                return Result(StaffInvitationStatus.Invalid);
            }

            if (await EmailHasAccountAsync(invitation.Email, cancellationToken))
            {
                return Result(StaffInvitationStatus.EmailTaken);
            }

            database.StaffInvitations.Add(StaffInvitation.Create(invitation, administratorId, Now()));
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            mail.Request();
            return Result(StaffInvitationStatus.Succeeded);
        }
        catch (DbUpdateException exception) when (IsConstraint(exception, PendingEmailIndex))
        {
            await RollBackAsync(transaction, cancellationToken);
            return Result(StaffInvitationStatus.EmailTaken);
        }
    }

    public async Task<StaffInvitationResult> ResendAsync(Guid administratorId, Guid invitationId,
        CancellationToken cancellationToken)
    {
        return await ChangeLinkAsync(administratorId, invitationId, null, cancellationToken);
    }

    public async Task<StaffInvitationResult> ChangeEmailAsync(Guid administratorId, Guid invitationId, string email,
        CancellationToken cancellationToken)
    {
        return await ChangeLinkAsync(administratorId, invitationId, email, cancellationToken);
    }

    // A resend and an email change are the same act: a new link, a renewed lifetime and the same send limits.
    private async Task<StaffInvitationResult> ChangeLinkAsync(Guid administratorId, Guid invitationId, string? email,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            if (!await IsAdministratorAsync(administratorId, cancellationToken))
            {
                return Result(StaffInvitationStatus.Forbidden);
            }

            var invitation = await LockAsync(invitationId, cancellationToken);

            if (invitation is null)
            {
                return Result(StaffInvitationStatus.NotFound);
            }

            if (invitation.ActivatedAt is not null || invitation.RevokedAt is not null)
            {
                return Result(StaffInvitationStatus.Unavailable);
            }

            var now = Now();
            var recent = invitation.SentAt.Where(sent => sent > now - EmailVerificationPolicy.ResendWindow)
                .OrderByDescending(sent => sent).ToArray();
            var delay = EmailVerificationPolicy.ResendDelay(now, recent);

            if (delay is not null)
            {
                return new StaffInvitationResult(StaffInvitationStatus.RateLimited, delay);
            }

            if (email is not null)
            {
                if (await EmailHasAccountAsync(email, cancellationToken))
                {
                    return Result(StaffInvitationStatus.EmailTaken);
                }

                invitation.Email = email;
            }

            invitation.Renew(now);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            mail.Request();
            return Result(StaffInvitationStatus.Succeeded);
        }
        catch (DbUpdateException exception) when (IsConstraint(exception, PendingEmailIndex))
        {
            await RollBackAsync(transaction, cancellationToken);
            return Result(StaffInvitationStatus.EmailTaken);
        }
    }

    public async Task<StaffInvitationResult> RevokeAsync(Guid administratorId, Guid invitationId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        if (!await IsAdministratorAsync(administratorId, cancellationToken))
        {
            return Result(StaffInvitationStatus.Forbidden);
        }

        var invitation = await LockAsync(invitationId, cancellationToken);

        if (invitation is null)
        {
            return Result(StaffInvitationStatus.NotFound);
        }

        if (invitation.ActivatedAt is not null)
        {
            return Result(StaffInvitationStatus.Unavailable);
        }

        if (invitation.RevokedAt is null)
        {
            invitation.RevokedAt = Now();
            invitation.RevokedById = administratorId;
            await database.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return Result(StaffInvitationStatus.Succeeded);
    }

    public async Task<PublicStaffInvitationResult> ReadPublicAsync(string token, CancellationToken cancellationToken)
    {
        if (!StaffInvitationToken.TryReadId(token, out var invitationId))
        {
            return new PublicStaffInvitationResult(StaffInvitationStatus.Unavailable);
        }

        var invitation = await database.StaffInvitations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == invitationId, cancellationToken);

        if (invitation is null || !invitation.IsPending(Now()) || !tokens.Verify(token, invitation))
        {
            return new PublicStaffInvitationResult(StaffInvitationStatus.Unavailable);
        }

        var branchName = invitation.BranchId is null ? null : await database.Branches.AsNoTracking()
            .Where(branch => branch.Id == invitation.BranchId).Select(branch => branch.Name)
            .SingleOrDefaultAsync(cancellationToken);
        return new PublicStaffInvitationResult(StaffInvitationStatus.Succeeded,
            new PublicStaffInvitation(invitation.Name, invitation.Role, branchName));
    }

    public async Task<StaffInvitationResult> ActivateAsync(string token, string password, string username,
        CancellationToken cancellationToken)
    {
        if (!StaffInvitationToken.TryReadId(token, out var invitationId))
        {
            return Result(StaffInvitationStatus.Unavailable);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var invitation = await LockAsync(invitationId, cancellationToken);

            if (invitation is null || !tokens.Verify(token, invitation))
            {
                return Result(StaffInvitationStatus.Unavailable);
            }

            if (invitation.ActivatedUserId is { } activatedId)
            {
                return Result(await IsSameActivationAsync(activatedId, password, username, cancellationToken)
                    ? StaffInvitationStatus.Succeeded : StaffInvitationStatus.Unavailable);
            }

            var now = Now();

            if (!invitation.IsPending(now) || (invitation.BranchId is { } branchId && !await database.Branches.AnyAsync(
                    branch => branch.Id == branchId && branch.IsActive, cancellationToken)))
            {
                return Result(StaffInvitationStatus.Unavailable);
            }

            if (await EmailHasAccountAsync(invitation.Email, cancellationToken))
            {
                return Result(StaffInvitationStatus.EmailTaken);
            }

            // Activation from the emailed link counts as email verification.
            var user = new AccountUser
            {
                Id = Guid.NewGuid(),
                UserName = invitation.Email,
                Email = invitation.Email,
                AccountUsername = username,
                EmailConfirmed = true,
                Role = invitation.Role,
                BranchId = invitation.BranchId
            };
            var created = await users.CreateAsync(user, password);

            if (!created.Succeeded)
            {
                await RollBackAsync(transaction, cancellationToken);
                return Result(created.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName")
                    ? StaffInvitationStatus.EmailTaken : StaffInvitationStatus.Invalid);
            }

            database.StaffProfiles.Add(new StaffProfile(user.Id, invitation.Name, invitation.Mobile));
            invitation.ActivatedAt = now;
            invitation.ActivatedUserId = user.Id;
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result(StaffInvitationStatus.Succeeded);
        }
        catch (DbUpdateException exception) when (IsConstraint(exception, UsernameIndex))
        {
            await RollBackAsync(transaction, cancellationToken);
            return Result(StaffInvitationStatus.UsernameTaken);
        }
        catch (DbUpdateException exception) when (IsConstraint(exception, EmailIndex))
        {
            await RollBackAsync(transaction, cancellationToken);
            return Result(StaffInvitationStatus.EmailTaken);
        }
    }

    private async Task<bool> IsSameActivationAsync(Guid userId, string password, string username,
        CancellationToken cancellationToken)
    {
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        return user?.PasswordHash is not null && user.AccountUsername == username &&
            passwords.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
    }

    private Task<bool> IsAdministratorAsync(Guid userId, CancellationToken cancellationToken)
    {
        return database.Users.AsNoTracking().AnyAsync(user => user.Id == userId && user.IsActive &&
            user.Role == AccountRoles.Administrator, cancellationToken);
    }

    private Task<bool> EmailHasAccountAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = email.ToUpperInvariant();
        return database.Users.AsNoTracking().AnyAsync(user => user.NormalizedEmail == normalized, cancellationToken);
    }

    private Task<StaffInvitation?> LockAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        return database.StaffInvitations.FromSqlInterpolated(
            $"SELECT * FROM \"StaffInvitations\" WHERE \"Id\" = {invitationId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static bool Matches(StaffInvitation existing, ValidatedStaffInvitation requested)
    {
        return existing.Email == requested.Email && existing.Role == requested.Role &&
            existing.BranchId == requested.BranchId && existing.Name == requested.Name &&
            existing.Mobile == requested.Mobile;
    }

    private static bool IsConstraint(DbUpdateException exception, string name)
    {
        return exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation } violation && violation.ConstraintName == name;
    }

    private async Task RollBackAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);
        database.ChangeTracker.Clear();
    }

    private static StaffInvitationResult Result(StaffInvitationStatus status) => new(status);

    private DateTimeOffset Now() => UtcTime.TruncateToMicroseconds(clock.GetUtcNow());
}
