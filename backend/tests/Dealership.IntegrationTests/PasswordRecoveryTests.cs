using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class PasswordRecoveryTests(RegistrationDatabase database) : IClassFixture<RegistrationDatabase>
{
    [Fact]
    public async Task GenericRequestsAndDurableQuotasPreserveLatestLinkAcrossRestart()
    {
        var clock = new RecoveryClock();
        RegistrationRequest account;
        PasswordResetChallenge latest;
        string firstToken;

        using (var host = new PasswordRecoveryHost(database, clock))
        {
            account = await host.RegisterAsync();
            var malformed = await host.Client.PostAsync("/api/auth/password-recovery",
                new StringContent("{", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
            Assert.True(malformed.Headers.CacheControl?.NoStore);
            var known = await host.Client.PostAsJsonAsync("/api/auth/password-recovery",
                new PasswordRecoveryRequest($"  {account.Email!.ToUpperInvariant()}  "));
            var unknown = await host.Client.PostAsJsonAsync("/api/auth/password-recovery",
                new PasswordRecoveryRequest($"aina.{Guid.NewGuid():N}@example.invalid"));
            var throttled = await host.Client.PostAsJsonAsync("/api/auth/password-recovery", new PasswordRecoveryRequest(account.Email));
            Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
            Assert.Equal(known.StatusCode, unknown.StatusCode);
            Assert.Equal(known.StatusCode, throttled.StatusCode);
            Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
            Assert.Equal(await known.Content.ReadAsStringAsync(), await throttled.Content.ReadAsStringAsync());
            Assert.True(known.Headers.CacheControl?.NoStore);
            Assert.True(unknown.Headers.CacheControl?.NoStore);
            Assert.True(throttled.Headers.CacheControl?.NoStore);
            Assert.Null(throttled.Headers.RetryAfter);
            var first = await host.LatestAsync(account.Email);
            firstToken = host.Token(first);
            Assert.Equal(TimeSpan.FromHours(1), first.ExpiresAt - first.CreatedAt);
            await host.RequestAsync(account.Email);
            Assert.Equal(first.Id, (await host.LatestAsync(account.Email)).Id);
            clock.Now += TimeSpan.FromMinutes(1);
            await Task.WhenAll(host.RequestAsync(account.Email), host.RequestAsync(account.Email));
            latest = await host.LatestAsync(account.Email);
            Assert.NotEqual(first.Id, latest.Id);
            Assert.Equal(PasswordResetStatus.Invalid, (await host.ResetAsync(firstToken)).Status);
        }

        using var restarted = new PasswordRecoveryHost(database, clock);
        await restarted.RequestAsync(account.Email!);
        Assert.Equal(latest.Id, (await restarted.LatestAsync(account.Email!)).Id);
        clock.Now += TimeSpan.FromMinutes(1);
        await restarted.RequestAsync(account.Email!);
        latest = await restarted.LatestAsync(account.Email!);
        clock.Now += TimeSpan.FromMinutes(1);
        await restarted.RequestAsync(account.Email!);
        Assert.Equal(latest.Id, (await restarted.LatestAsync(account.Email!)).Id);
        using (var scope = restarted.Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            Assert.Equal(3, await context.PasswordResetChallenges.CountAsync(challenge => challenge.UserId == latest.UserId));
        }

        clock.Now = latest.CreatedAt + TimeSpan.FromHours(1);
        Assert.Equal(PasswordResetStatus.Invalid, (await restarted.ResetAsync(restarted.Token(latest))).Status);
        await restarted.RequestAsync(account.Email!);
        Assert.NotEqual(latest.Id, (await restarted.LatestAsync(account.Email!)).Id);
    }

    [Fact]
    public async Task ResetRevokesOnlyOwnerAndExactReplayPreservesLaterSessionsAcrossRestart()
    {
        var clock = new RecoveryClock();
        RegistrationRequest account;
        string token;
        LoginSession later;
        LoginSession unrelated;
        PasswordResetChallenge completed;
        AccountUser credentials;

        using (var host = new PasswordRecoveryHost(database, clock))
        {
            account = await host.RegisterAsync();
            var other = await host.RegisterAsync();
            var original = (await host.LoginAsync(account))!;
            Assert.NotNull(await host.LoginAsync(account));
            var revoked = (await host.LoginAsync(account))!;
            unrelated = (await host.LoginAsync(other))!;
            host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", revoked.AccessToken);
            Assert.Equal(HttpStatusCode.NoContent, (await host.Client.PostAsync("/api/auth/logout", null)).StatusCode);
            clock.Now += TimeSpan.FromSeconds(10);
            await host.RequestAsync(account.Email!);
            var challenge = await host.LatestAsync(account.Email!);
            token = host.Token(challenge);
            var response = await host.Client.PostAsJsonAsync("/api/auth/password-reset", new PasswordResetRequest(token, "Recovered123!"));
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.DoesNotContain(response.Headers, header => header.Key == "Set-Cookie");
            host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", original.AccessToken);
            Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/account")).StatusCode);
            Assert.Null(await host.LoginAsync(account));
            later = (await host.LoginAsync(account, "Recovered123!"))!;
            completed = await host.LatestAsync(account.Email!);
            Assert.NotNull(completed.ConsumedAt);
            using var credentialScope = host.Factory.Services.CreateScope();
            credentials = await credentialScope.ServiceProvider.GetRequiredService<DealershipDbContext>().Users.AsNoTracking()
                .SingleAsync(user => user.Id == completed.UserId);
        }

        using var restarted = new PasswordRecoveryHost(database, clock);
        clock.Now += TimeSpan.FromMinutes(1);
        Assert.Equal(PasswordResetStatus.Succeeded, (await restarted.ResetAsync(token)).Status);
        Assert.Equal(completed.CompletedSecurityStamp, (await restarted.LatestAsync(account.Email!)).CompletedSecurityStamp);
        Assert.Equal(completed.ConsumedAt, (await restarted.LatestAsync(account.Email!)).ConsumedAt);
        using (var replayScope = restarted.Factory.Services.CreateScope())
        {
            var persisted = await replayScope.ServiceProvider.GetRequiredService<DealershipDbContext>().Users.AsNoTracking()
                .SingleAsync(user => user.Id == completed.UserId);
            Assert.True(credentials.PasswordHash == persisted.PasswordHash && credentials.SecurityStamp == persisted.SecurityStamp &&
                credentials.ConcurrencyStamp == persisted.ConcurrencyStamp, "Exact replay must not update credentials or concurrency state.");
        }

        restarted.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", later.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await restarted.Client.GetAsync("/api/account")).StatusCode);
        restarted.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", unrelated.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await restarted.Client.GetAsync("/api/account")).StatusCode);
        Assert.Equal(PasswordResetStatus.Invalid, (await restarted.ResetAsync(token, "Changed123!")).Status);
        await restarted.RequestAsync(account.Email!);
        Assert.Equal(PasswordResetStatus.Succeeded,
            (await restarted.ResetAsync(restarted.Token(await restarted.LatestAsync(account.Email!)))).Status);
        Assert.Equal(PasswordResetStatus.Invalid, (await restarted.ResetAsync(token)).Status);
        using var scope = restarted.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var owner = await context.Users.AsNoTracking().SingleAsync(user => user.Email == account.Email);
        Assert.False(owner.EmailConfirmed);
        Assert.True(owner.IsActive);
        Assert.Equal(AccountRoles.Customer, owner.Role);
        Assert.Null(owner.BranchId);
        var receipts = await context.AccountSessions.Where(session => session.UserId == owner.Id)
            .OrderBy(session => session.RevokedAt).ToListAsync();
        Assert.Equal(4, receipts.Count);
        Assert.Equal(2, receipts.Count(session => session.RevokedAt == completed.ConsumedAt));
        Assert.Single(receipts, session => session.RevokedAt < completed.ConsumedAt);
    }

    [Fact]
    public async Task TokenPurposeOwnershipCurrentEmailAndConfiguredPolicyAreEnforced()
    {
        using var host = new PasswordRecoveryHost(database);
        var account = await host.RegisterAsync();
        var other = await host.RegisterAsync();
        await host.RequestAsync(account.Email!);
        var challenge = await host.LatestAsync(account.Email!);
        var token = host.Token(challenge);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await host.Client.GetAsync("/api/auth/password-reset")).StatusCode);
        var rejected = await host.Client.PostAsJsonAsync("/api/auth/password-reset", new PasswordResetRequest(token, "short"));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.True(rejected.Headers.CacheControl?.NoStore);
        Assert.Contains("password", await rejected.Content.ReadAsStringAsync());
        var forged = token[..^1] + (token[^1] == 'a' ? 'b' : 'a');
        Assert.Equal(PasswordResetStatus.Invalid, (await host.ResetAsync(forged)).Status);
        var invalidResponse = await host.Client.PostAsJsonAsync("/api/auth/password-reset", new PasswordResetRequest(forged, "Recovered123!"));
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        Assert.True(invalidResponse.Headers.CacheControl?.NoStore);
        Assert.Contains("Reset link is invalid or expired.", await invalidResponse.Content.ReadAsStringAsync());
        using (var scope = host.Factory.Services.CreateScope())
        {
            var verificationTokens = scope.ServiceProvider.GetRequiredService<EmailVerificationToken>();
            var wrongPurpose = verificationTokens.Create(new EmailVerificationChallenge
            {
                Id = challenge.Id,
                UserId = challenge.UserId,
                ExpiresAt = challenge.ExpiresAt
            });
            Assert.Equal(PasswordResetStatus.Invalid, (await host.ResetAsync(wrongPurpose)).Status);
        }

        foreach (var invalid in new[] { "short", "NoDigits!", "nouppercase123!", "NOLOWERCASE123!", "NoSymbol123 ", new string('A', 129) })
        {
            Assert.NotEqual(PasswordResetStatus.Succeeded, (await host.ResetAsync(token, invalid)).Status);
        }

        Assert.NotNull(await host.LoginAsync(account));
        Assert.NotNull(await host.LoginAsync(other));
        Assert.Null((await host.LatestAsync(account.Email!)).ConsumedAt);
        using (var policyScope = host.Factory.Services.CreateScope())
        {
            var users = policyScope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
            users.PasswordValidators.Add(new RejectingPasswordValidator());
            var result = await policyScope.ServiceProvider.GetRequiredService<PasswordRecoveryService>()
                .ResetAsync(new PasswordResetRequest(token, "Recovered123!"), CancellationToken.None);
            Assert.Equal(PasswordResetStatus.PasswordRejected, result.Status);
            Assert.NotEmpty(result.PasswordErrors!);
        }

        using (var scope = host.Factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
            var user = (await users.FindByEmailAsync(account.Email!))!;
            Assert.True((await users.SetEmailAsync(user, $"changed.{Guid.NewGuid():N}@example.invalid")).Succeeded);
        }

        Assert.Equal(PasswordResetStatus.Invalid, (await host.ResetAsync(token)).Status);
    }

    [Fact]
    public async Task DisabledStaffReceiveNoRecoveryLinkAndNothingChanges()
    {
        using var host = new PasswordRecoveryHost(database);
        using (var scope = host.Factory.Services.CreateScope())
        {
            var fixture = scope.ServiceProvider.GetRequiredService<StaffProvisioningService>();
            await scope.ServiceProvider.GetRequiredService<BranchProvisioningService>()
                .ProvisionBranchAsync(DevelopmentFixtureManifest.Branches[0], CancellationToken.None);
            await fixture.ProvisionStaffAsync(DevelopmentFixtureManifest.Staff[0], CancellationToken.None);
            var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            await context.Users.Where(user => user.Id == DevelopmentFixtureManifest.Staff[0].Id)
                .ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, false).SetProperty(user => user.EmailConfirmed, false));
        }

        var staff = DevelopmentFixtureManifest.Staff[0];
        await host.RequestAsync(staff.Email!);
        // Suspended and deactivated accounts get no link, and nothing about them changes.
        using var verification = host.Factory.Services.CreateScope();
        var challenges = verification.ServiceProvider.GetRequiredService<DealershipDbContext>().PasswordResetChallenges;
        Assert.False(await challenges.AnyAsync(challenge => challenge.UserId == staff.Id));
        var users = verification.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
        var persisted = (await users.FindByIdAsync(staff.Id.ToString()))!;
        Assert.Equal(staff.Role, persisted.Role);
        Assert.Equal(staff.BranchId, persisted.BranchId);
        Assert.False(persisted.IsActive);
        Assert.False(persisted.EmailConfirmed);
    }

    private sealed class RejectingPasswordValidator : IPasswordValidator<AccountUser>
    {
        public Task<IdentityResult> ValidateAsync(UserManager<AccountUser> manager, AccountUser user, string? password)
        {
            return Task.FromResult(IdentityResult.Failed());
        }
    }
}
