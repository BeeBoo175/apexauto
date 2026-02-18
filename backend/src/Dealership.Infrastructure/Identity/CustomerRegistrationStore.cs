using Dealership.Application.Accounts;
using Dealership.Domain.Customers;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Identity;

public sealed class CustomerRegistrationStore(DealershipDbContext database, UserManager<AccountUser> users,
    IEmailVerificationStore verification, MailDeliverySignal mail)
    : ICustomerRegistrationStore
{
    private static readonly RegistrationResult Success = new(RegistrationStatus.Succeeded, new Dictionary<string, string[]>());

    private static readonly RegistrationResult Conflict = new(RegistrationStatus.Conflict,
        new Dictionary<string, string[]> { ["account"] = ["Registration conflicts with an existing account or request. Use existing-account recovery."] });

    public async Task<RegistrationResult> RegisterAsync(ValidatedRegistration registration, CancellationToken cancellationToken)
    {
        var replay = await FindReplayAsync(registration, cancellationToken);

        if (replay is not null)
        {
            return replay;
        }

        // Identity and profile writes share the scoped DbContext, so neither can commit alone.
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var user = new AccountUser
        {
            Id = Guid.NewGuid(),
            UserName = registration.Email,
            Email = registration.Email,
            EmailConfirmed = false
        };

        try
        {
            var created = await users.CreateAsync(user, registration.Password);

            if (!created.Succeeded)
            {
                if (created.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    database.ChangeTracker.Clear();
                    return await FindReplayAsync(registration, cancellationToken) ?? Conflict;
                }

                var errors = created.Errors.GroupBy(error => error.Code)
                    .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());
                return new RegistrationResult(RegistrationStatus.Invalid, errors);
            }

            database.CustomerProfiles.Add(new CustomerProfile(user.Id, registration.RegistrationKey,
                registration.LegalName, registration.Nric, registration.Mobile, registration.ResidentialAddress));
            await database.SaveChangesAsync(cancellationToken);
            await verification.QueueInitialAsync(user.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            mail.Request();
            return Success;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // PostgreSQL aborts a transaction on constraint failure; roll back before reading the winner.
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            return await FindReplayAsync(registration, cancellationToken) ?? Conflict;
        }
    }

    private async Task<RegistrationResult?> FindReplayAsync(ValidatedRegistration registration, CancellationToken cancellationToken)
    {
        var profile = await database.CustomerProfiles.AsNoTracking()
            .SingleOrDefaultAsync(profile => profile.RegistrationKey == registration.RegistrationKey, cancellationToken);

        if (profile is null)
        {
            return null;
        }

        // Replay uses current account/profile values and Identity verification, never a fast password fingerprint.
        var user = await users.FindByIdAsync(profile.UserId.ToString());
        var matches = user is not null &&
            users.NormalizeEmail(user.Email) == users.NormalizeEmail(registration.Email) &&
            profile.LegalName == registration.LegalName && profile.Nric == registration.Nric &&
            profile.Mobile == registration.Mobile && profile.ResidentialAddress == registration.ResidentialAddress &&
            await users.CheckPasswordAsync(user, registration.Password);
        return matches ? Success : Conflict;
    }
}
