using Dealership.Application.Accounts;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

// Careless, confused and hostile use of customer closure and Administrator erasure, against the restricted runtime role.
public sealed class AdversarialErasureTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    private const string Password = AdversarialWorld.AdministratorPassword;

    [Fact]
    public async Task AnOperationIdNeverErasesAnotherAccountOrReplaysForAnotherActor()
    {
        // Guarantee: an erasure receipt is bound to its kind, target and actor; reusing the id cannot erase another account.
        using var world = AdversarialWorld.Create(fixture);
        var first = await world.AdministratorAsync();
        var second = await world.AdministratorAsync();
        var (x, _) = await world.CustomerAsync();
        var (y, _) = await world.CustomerAsync();
        var operationId = Guid.NewGuid();

        Assert.Equal(AccountErasureStatus.Erased, (await world.EraseCustomerAsync(first, x, operationId, Password)).Status);
        // Control: the same command with a changed reason is a replay, which proves the refusals below are not a broken path.
        Assert.Equal(AccountErasureStatus.Erased,
            (await world.EraseCustomerAsync(first, x, operationId, Password, "A different reason")).Status);

        Assert.Equal(AccountErasureStatus.Conflict, (await world.EraseCustomerAsync(first, y, operationId, Password)).Status);
        Assert.True(await world.IsOpenAsync(y));
        // A customer reusing that id for their own closure is refused too, not answered with a server error.
        var (z, zRegistration) = await world.CustomerAsync();
        Assert.Equal(CustomerClosureStatus.Conflict, (await world.CloseAsync(z, operationId, zRegistration.Password)).Status);
        Assert.True(await world.IsOpenAsync(z));
        Assert.NotEqual(AccountErasureStatus.Erased, (await world.EraseCustomerAsync(second, x, operationId, Password)).Status);
        Assert.NotEqual(AccountErasureStatus.Erased, (await world.EraseCustomerAsync(second, y, operationId, Password)).Status);
        Assert.True(await world.IsOpenAsync(y));
        Assert.Equal(1, await world.ErasedEventCountAsync(x));
        Assert.Equal(0, await world.ErasedEventCountAsync(y));
    }

    [Fact]
    public async Task WrongEmptyAndForeignPasswordsChangeNothingAndOnlyTheRightOneErases()
    {
        // Guarantee: the confirming password must be the acting user's own; no other credential or blank input erases.
        using var world = AdversarialWorld.Create(fixture);
        var admin = await world.AdministratorAsync();
        var (customerId, registration) = await world.CustomerAsync();

        foreach (var password in new[] { "", " ", "Wrong-Password1!", registration.Password, new string('x', 5000) })
        {
            var result = await world.EraseCustomerAsync(admin, customerId, Guid.NewGuid(), password!);
            Assert.NotEqual(AccountErasureStatus.Erased, result.Status);
        }

        // The customer's own password, supplied by the Administrator, is the "another user's password" case.
        Assert.Equal(AccountErasureStatus.WrongPassword,
            (await world.EraseCustomerAsync(admin, customerId, Guid.NewGuid(), registration.Password)).Status);
        Assert.Equal(AccountErasureStatus.Invalid,
            (await world.EraseCustomerAsync(admin, customerId, Guid.NewGuid(), Password, new string('r', 1001))).Status);
        Assert.Equal(AccountErasureStatus.Invalid,
            (await world.EraseCustomerAsync(admin, customerId, Guid.Empty, Password)).Status);
        Assert.Equal(AccountErasureStatus.Invalid,
            (await world.EraseCustomerAsync(admin, customerId, Guid.NewGuid(), Password, "bad\u0007reason")).Status);
        Assert.True(await world.IsOpenAsync(customerId));

        // The administrator's password does not close the customer's own account either.
        Assert.Equal(CustomerClosureStatus.WrongPassword, (await world.CloseAsync(customerId, Guid.NewGuid(), Password)).Status);
        Assert.Equal(CustomerClosureStatus.Invalid, (await world.CloseAsync(customerId, Guid.NewGuid(), "")).Status);
        Assert.True(await world.IsOpenAsync(customerId));

        Assert.Equal(AccountErasureStatus.Erased, (await world.EraseCustomerAsync(admin, customerId, Guid.NewGuid(), Password)).Status);
        Assert.False(await world.IsOpenAsync(customerId));
    }

    [Fact]
    public async Task AStaleSecurityStampRefusesTheLockedCommandAndLeavesTheAccountOpen()
    {
        // Guarantee: credentials changed between the password check and the lock cancel the closure, not complete it.
        using var world = AdversarialWorld.Create(fixture);
        var admin = await world.AdministratorAsync();
        var (customerId, _) = await world.CustomerAsync();
        var (otherId, _) = await world.CustomerAsync();
        var stale = (await world.UserAsync(customerId)).SecurityStamp!;
        await world.RunAsync(async services =>
        {
            await services.GetRequiredService<DealershipDbContext>().Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"AspNetUsers\" SET \"SecurityStamp\" = 'rotated-after-check' WHERE \"Id\" = {customerId}");
            return true;
        });

        var refused = await world.RunAsync(services => services.GetRequiredService<ICustomerClosureStore>()
            .CloseAsync(customerId, Guid.NewGuid(), stale, CancellationToken.None));
        Assert.Equal(CustomerClosureStatus.SessionChanged, refused.Status);
        Assert.True(await world.IsOpenAsync(customerId));

        // Control: the current stamp closes, so the refusal came from the stamp and nothing else.
        var current = (await world.UserAsync(customerId)).SecurityStamp!;
        var closed = await world.RunAsync(services => services.GetRequiredService<ICustomerClosureStore>()
            .CloseAsync(customerId, Guid.NewGuid(), current, CancellationToken.None));
        Assert.Equal(CustomerClosureStatus.Closed, closed.Status);

        // An Administrator whose stamp is stale cannot erase either, and a stamp of the wrong user is not accepted.
        var adminStamp = (await world.UserAsync(admin)).SecurityStamp!;
        var other = await world.RunAsync(services => services.GetRequiredService<IAccountErasureStore>().EraseCustomerAsync(
            admin, otherId, new(Guid.NewGuid(), "Subject request", Password), "not-the-admin-stamp", CancellationToken.None));
        Assert.Equal(AccountErasureStatus.SessionChanged, other.Status);
        Assert.True(await world.IsOpenAsync(otherId));
        Assert.NotEmpty(adminStamp);
    }

    [Fact]
    public async Task CustomerSelfClosureRacingAnAdministratorErasureLeavesOneReceipt()
    {
        // Guarantee: two concurrent erasures of one account produce exactly one durable erasure and no unhandled failure.
        using var world = AdversarialWorld.Create(fixture);
        var admin = await world.AdministratorAsync();

        for (var round = 0; round < 6; round++)
        {
            var (customerId, registration) = await world.CustomerAsync();
            var own = Guid.NewGuid();
            var theirs = Guid.NewGuid();
            var closing = world.CloseAsync(customerId, own, registration.Password);
            var erasing = world.EraseCustomerAsync(admin, customerId, theirs, Password);
            var closingResult = await closing;
            var erasingResult = await erasing;

            Assert.False(await world.IsOpenAsync(customerId));
            Assert.Equal(1, await world.ErasedEventCountAsync(customerId));
            Assert.True(closingResult.Status is CustomerClosureStatus.Closed or CustomerClosureStatus.Forbidden or
                CustomerClosureStatus.RetryLater or CustomerClosureStatus.SessionChanged or CustomerClosureStatus.WrongPassword,
                closingResult.Status.ToString());
            Assert.True(erasingResult.Status is AccountErasureStatus.Erased or AccountErasureStatus.NotFound or
                AccountErasureStatus.RetryLater or AccountErasureStatus.Conflict, erasingResult.Status.ToString());
            Assert.True(closingResult.Status == CustomerClosureStatus.Closed ||
                erasingResult.Status == AccountErasureStatus.Erased);
        }
    }

    [Fact]
    public async Task AnAdministratorCannotEraseAStaffMemberThroughTheCustomerPathOrTheReverse()
    {
        // Guarantee: each erasure command only reaches its own kind of account; identity is never guessed from the id.
        using var world = AdversarialWorld.Create(fixture);
        var admin = await world.AdministratorAsync();
        var peer = await world.AdministratorAsync();
        var (customerId, _) = await world.CustomerAsync();

        Assert.Equal(AccountErasureStatus.NotFound, (await world.EraseCustomerAsync(admin, peer, Guid.NewGuid(), Password)).Status);
        Assert.Equal(AccountErasureStatus.NotFound, (await world.EraseStaffAsync(admin, customerId, Guid.NewGuid(), Password)).Status);
        Assert.Equal(AccountErasureStatus.NotFound, (await world.EraseCustomerAsync(admin, Guid.NewGuid(), Guid.NewGuid(), Password)).Status);
        Assert.Equal(AccountErasureStatus.Refused, (await world.EraseStaffAsync(admin, admin, Guid.NewGuid(), Password)).Status);
        // A customer holds no Administrator capability, whatever id and password they send.
        Assert.Equal(AccountErasureStatus.Forbidden, (await world.EraseCustomerAsync(customerId, customerId, Guid.NewGuid(), Password)).Status);
        Assert.True(await world.IsOpenAsync(peer));
        Assert.True(await world.IsOpenAsync(admin));
        Assert.True(await world.IsOpenAsync(customerId));
    }

    [Fact]
    public async Task ASuspendedCustomerCanBeErasedAndTheSuspensionCannotBeLiftedAfterwards()
    {
        // Guarantee: closure clears suspension state, and Administrator writers treat the erased account as gone.
        using var world = AdversarialWorld.Create(fixture);
        var admin = await world.AdministratorAsync();
        var (customerId, _) = await world.CustomerAsync();
        await world.RunAsync(async services =>
        {
            await services.GetRequiredService<DealershipDbContext>().Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"AspNetUsers\" SET \"SuspendedAt\" = now(), \"IsActive\" = false WHERE \"Id\" = {customerId}");
            return true;
        });

        Assert.Equal(AccountErasureStatus.Erased, (await world.EraseCustomerAsync(admin, customerId, Guid.NewGuid(), Password)).Status);
        var user = await world.UserAsync(customerId);
        Assert.Null(user.SuspendedAt);
        Assert.False(user.IsActive);
        Assert.Null(user.PasswordHash);
        Assert.Equal(AccountErasureStatus.NotFound, (await world.EraseCustomerAsync(admin, customerId, Guid.NewGuid(), Password)).Status);
    }

    [Fact]
    public async Task OldSecurityTokensDieWithTheAccountAndAFreshAccountReadsNothingOfIt()
    {
        // Guarantee: reset and verification links, sessions and credentials issued before erasure never revive the account,
        // and a new account on the released email and NRIC is a new identity with none of the old data.
        using var world = AdversarialWorld.Create(fixture);
        var admin = await world.AdministratorAsync();
        var (customerId, registration) = await world.CustomerAsync();
        await world.RunAsync(async services =>
        {
            await services.GetRequiredService<PasswordRecoveryService>()
                .RequestAsync(new PasswordRecoveryRequest(registration.Email), CancellationToken.None);
            return true;
        });
        var (resetToken, verifyToken) = await world.RunAsync(async services =>
        {
            var database = services.GetRequiredService<DealershipDbContext>();
            var reset = await database.PasswordResetChallenges.AsNoTracking().FirstAsync(item => item.UserId == customerId);
            var verification = await database.EmailVerificationChallenges.AsNoTracking()
                .FirstAsync(item => item.UserId == customerId);
            return (services.GetRequiredService<PasswordResetToken>().Create(reset),
                services.GetRequiredService<EmailVerificationToken>().Create(verification));
        });

        Assert.Equal(AccountErasureStatus.Erased, (await world.EraseCustomerAsync(admin, customerId, Guid.NewGuid(), Password)).Status);

        var reset = await world.RunAsync(services => services.GetRequiredService<PasswordRecoveryService>()
            .ResetAsync(new PasswordResetRequest(resetToken, "Revived-Password1!"), CancellationToken.None));
        Assert.NotEqual(PasswordResetStatus.Succeeded, reset.Status);
        var verified = await world.RunAsync(services => services.GetRequiredService<EmailVerificationService>()
            .ConfirmAsync(verifyToken, CancellationToken.None));
        Assert.NotEqual(VerificationStatus.Accepted, verified.Status);
        var closed = await world.UserAsync(customerId);
        Assert.Null(closed.PasswordHash);
        Assert.False(closed.EmailConfirmed);
        Assert.False(closed.IsActive);

        // A recovery request for the old address finds no account and creates no challenge for the closed one.
        await world.RunAsync(async services =>
        {
            await services.GetRequiredService<PasswordRecoveryService>()
                .RequestAsync(new PasswordRecoveryRequest(registration.Email), CancellationToken.None);
            return true;
        });
        var challenges = await world.RunAsync(services => services.GetRequiredService<DealershipDbContext>()
            .PasswordResetChallenges.AsNoTracking().CountAsync(item => item.UserId == customerId));
        Assert.Equal(1, challenges);

        // The old registration request, replayed, never signs anyone in as the old account or reopens it.
        var replay = await world.RunAsync(services => services.GetRequiredService<RegistrationService>()
            .RegisterAsync(registration, CancellationToken.None));
        Assert.True(replay.Status is RegistrationStatus.Succeeded or RegistrationStatus.Conflict, replay.Status.ToString());
        Assert.False(await world.IsOpenAsync(customerId));

        // A new registration on the same NRIC and a changed key is a different identity.
        var again = registration with
        {
            RegistrationKey = Guid.NewGuid(),
            Email = $"fresh.{Guid.NewGuid():N}@example.invalid",
            Username = TestUsernames.Next(),
            LegalName = "Fresh Owner"
        };
        var (freshId, _) = await world.CustomerAsync(again);
        Assert.NotEqual(customerId, freshId);
        var profiles = await world.RunAsync(services => services.GetRequiredService<DealershipDbContext>().CustomerProfiles
            .AsNoTracking().Where(item => item.UserId == customerId || item.UserId == freshId).ToListAsync());
        Assert.Null(profiles.Single(item => item.UserId == customerId).Nric);
        Assert.Equal(registration.Nric, profiles.Single(item => item.UserId == freshId).Nric);
        var blockers = await world.RunAsync(services => services.GetRequiredService<CustomerClosureService>()
            .BlockersAsync(freshId, CancellationToken.None));
        Assert.Empty(blockers!);
    }
}
