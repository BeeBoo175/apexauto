using Dealership.Application.Accounts;
using Dealership.Domain.Customers;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

public sealed class DevelopmentCustomerFixtures(DealershipDbContext database, RegistrationService registration,
    EmailVerificationService verification, EmailVerificationToken tokens, IPasswordHasher<AccountUser> passwords,
    TimeProvider clock)
{
    public async Task<DevelopmentFixtureReport> SeedAsync(DevelopmentCustomerFixture fixture,
        CancellationToken cancellationToken)
    {
        var requested = fixture.Registration;
        var label = $"Customer ({requested.Email})";
        var profile = await FindProfileAsync(requested.RegistrationKey, cancellationToken);
        var created = profile is null;

        if (created)
        {
            var result = await registration.RegisterAsync(requested, cancellationToken);

            if (result.Status != RegistrationStatus.Succeeded)
            {
                return new DevelopmentFixtureReport(label, "conflict; preserved", true);
            }

            profile = await FindProfileAsync(requested.RegistrationKey, cancellationToken);
        }

        if (profile is null)
        {
            throw new InvalidOperationException("Canonical registration did not persist the fixture profile.");
        }

        var user = await database.Users.AsNoTracking().SingleAsync(
            candidate => candidate.Id == profile.UserId, cancellationToken);

        if (!Matches(user, profile, requested))
        {
            return new DevelopmentFixtureReport(label, "drift; preserved", true);
        }

        if (fixture.VerifyEmail && !await ConfirmEmailAsync(user, cancellationToken))
        {
            return new DevelopmentFixtureReport(label, "verification requires attention; preserved", true);
        }

        return new DevelopmentFixtureReport(label, created ? "created" : "matches", false);
    }

    public async Task<DevelopmentFixtureReport> ReviewAsync(DevelopmentCustomerFixture fixture,
        CancellationToken cancellationToken)
    {
        var requested = fixture.Registration;
        var profile = await FindProfileAsync(requested.RegistrationKey, cancellationToken);
        var userId = profile?.UserId;
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == userId, cancellationToken);
        var matches = user is not null && profile is not null && Matches(user, profile, requested) &&
            user.EmailConfirmed == fixture.VerifyEmail;
        var state = user is null ? "missing" : "drift; preserved";
        return new DevelopmentFixtureReport($"Customer ({requested.Email})", matches ? "matches" : state, !matches);
    }

    private Task<CustomerProfile?> FindProfileAsync(Guid registrationKey, CancellationToken cancellationToken)
    {
        return database.CustomerProfiles.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.RegistrationKey == registrationKey, cancellationToken);
    }

    private bool Matches(AccountUser user, CustomerProfile profile, RegistrationRequest requested)
    {
        return user.Email == requested.Email && user.Role == AccountRoles.Customer &&
            user.BranchId is null && user.IsActive && profile.LegalName == requested.LegalName &&
            profile.Nric == requested.Nric && profile.Mobile == requested.Mobile &&
            profile.ResidentialAddress == requested.ResidentialAddress && user.PasswordHash is not null &&
            passwords.VerifyHashedPassword(user, user.PasswordHash, requested.Password!) != PasswordVerificationResult.Failed;
    }

    private async Task<bool> ConfirmEmailAsync(AccountUser user, CancellationToken cancellationToken)
    {
        if (user.EmailConfirmed)
        {
            return true;
        }

        // A consumed challenge proves prior confirmation; a later manual change is preserved.
        if (await database.EmailVerificationChallenges.AnyAsync(
            challenge => challenge.UserId == user.Id && challenge.ConsumedAt != null, cancellationToken))
        {
            return false;
        }

        var challenge = await FindChallengeAsync(user.Id, cancellationToken);

        if (challenge is null)
        {
            var resent = await verification.ResendAsync(user.Id, Guid.NewGuid(), cancellationToken);

            if (resent.Status != VerificationStatus.Accepted)
            {
                return false;
            }

            challenge = await FindChallengeAsync(user.Id, cancellationToken);
        }

        if (challenge is null)
        {
            return false;
        }

        // Consume the same signed token as local mail through the canonical verification command.
        var confirmed = await verification.ConfirmAsync(tokens.Create(challenge), cancellationToken);
        return confirmed.Status == VerificationStatus.Accepted;
    }

    private Task<EmailVerificationChallenge?> FindChallengeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return database.EmailVerificationChallenges.AsNoTracking()
            .Where(challenge => challenge.UserId == userId && challenge.ConsumedAt == null &&
                challenge.InvalidatedAt == null && challenge.ExpiresAt > now)
            .OrderByDescending(challenge => challenge.CreatedAt).ThenByDescending(challenge => challenge.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
