using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Dealership.Application.Accounts;
using Dealership.Domain.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

// HTTP-level misuse of the closure and erasure endpoints and of every Administrator writer against erased accounts.
// The credential budget is five per minute per host, so each scenario keeps to a few requests of that kind.
public sealed class AdversarialAccessTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task EveryAdministratorWriterTreatsAnErasedCustomerAndAnErasedEmployeeAsGone()
    {
        // Guarantee: no Administrator command can edit, suspend, reset, reveal, transfer or reactivate an erased account.
        using var factory = fixture.CreateFactory();
        using var world = new AdversarialWorld(factory);
        using var admin = factory.CreateClient();
        await StaffInvitationTests.SignInAdministratorAsync(factory, admin);
        var eraser = await world.AdministratorAsync();
        var (openCustomer, _) = await world.CustomerAsync();
        var (erasedCustomer, _) = await world.CustomerAsync();
        var (erasedStaff, branchId) = await ProvisionAgentAsync(world, factory);

        // Control: the same writers reach an open customer, so a refusal below cannot be a wrong route or body.
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync(
            $"/api/customer-accounts/{openCustomer}/nric-reveals", Reason())).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync(
            $"/api/customer-accounts/{openCustomer}/session-revocations", Reason())).StatusCode);

        Assert.Equal(AccountErasureStatus.Erased,
            (await world.EraseCustomerAsync(eraser, erasedCustomer, Guid.NewGuid(), AdversarialWorld.AdministratorPassword)).Status);
        Assert.Equal(AccountErasureStatus.Erased,
            (await world.EraseStaffAsync(eraser, erasedStaff, Guid.NewGuid(), AdversarialWorld.AdministratorPassword)).Status);

        foreach (var target in new[] { erasedCustomer, erasedStaff })
        {
            var attempts = new (string Path, object Body)[]
            {
                ($"/api/accounts/{target}/suspensions", new { operationId = Guid.NewGuid(), reason = "Support check",
                    requirePasswordReset = false, requireEmailVerification = false }),
                ($"/api/accounts/{target}/suspensions/lift", new { operationId = Guid.NewGuid(), reason = "Support check",
                    requirePasswordReset = false, requireEmailVerification = false }),
                ($"/api/accounts/{target}/edits", new { operationId = Guid.NewGuid(), reason = "Support check",
                    name = "Restored Name", mobile = "0123456780", address = "1 Jalan Pulih, Shah Alam",
                    email = $"restored.{Guid.NewGuid():N}@example.invalid", requirePasswordReset = false,
                    requireEmailVerification = false }),
                ($"/api/customer-accounts/{target}/nric-reveals", Reason()),
                ($"/api/customer-accounts/{target}/session-revocations", Reason()),
                ($"/api/customer-accounts/{target}/password-resets", Reason()),
                ($"/api/customer-accounts/{target}/verification-resends", Reason()),
                ($"/api/staff-accounts/{target}/deactivations", Reason()),
                ($"/api/staff-accounts/{target}/reactivations", new { operationId = Guid.NewGuid() }),
                ($"/api/staff-accounts/{target}/transfers", new { operationId = Guid.NewGuid(), reason = "Support check", branchId })
            };

            foreach (var (path, body) in attempts)
            {
                var response = await admin.PostAsJsonAsync(path, body);
                Assert.True(!response.IsSuccessStatusCode && (int)response.StatusCode < 500,
                    $"{path} answered {(int)response.StatusCode}");
            }

            var user = await world.UserAsync(target);
            Assert.False(user.IsActive);
            Assert.Null(user.Email);
            Assert.Null(user.PasswordHash);
            Assert.NotNull(user.ClosedAt);
            Assert.Equal(1, await EventCountAsync(world, target));
        }
    }

    [Fact]
    public async Task AnOldBearerNeverReachesAnythingAfterErasureAndAnExpiredSessionCannotClose()
    {
        // Guarantee: access ends with the erasure for a token issued before it, and a session that ended mid-flow
        // cannot complete a closure.
        using var factory = fixture.CreateFactory();
        using var world = new AdversarialWorld(factory);
        var eraser = await world.AdministratorAsync();
        using var customer = factory.CreateClient();
        var registration = await IntakePhotoFixture.RegisterAsync(customer);
        var customerId = await world.UserIdAsync(registration.Email!);

        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync("/api/account")).StatusCode);
        Assert.Equal(AccountErasureStatus.Erased,
            (await world.EraseCustomerAsync(eraser, customerId, Guid.NewGuid(), AdversarialWorld.AdministratorPassword)).Status);

        foreach (var path in new[] { "/api/account", "/api/account/profile", "/api/auth/session",
            "/api/account/closure/blockers" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await customer.GetAsync(path)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await customer.PostAsJsonAsync("/api/account/closure",
            new { operationId = Guid.NewGuid(), password = registration.Password })).StatusCode);
    }

    [Fact]
    public async Task ASessionRevokedMidFlowCannotConfirmTheClosureAndTheAccountStaysOpen()
    {
        // Guarantee: a confirmation arriving after the session ended answers 401 and changes nothing.
        using var factory = fixture.CreateFactory();
        using var world = new AdversarialWorld(factory);
        using var customer = factory.CreateClient();
        var registration = await IntakePhotoFixture.RegisterAsync(customer);
        var customerId = await world.UserIdAsync(registration.Email!);
        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync("/api/account/closure/blockers")).StatusCode);

        await world.RunAsync(async services =>
        {
            await services.GetRequiredService<DealershipDbContext>().Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"AccountSessions\" SET \"RevokedAt\" = now() WHERE \"UserId\" = {customerId}");
            return true;
        });

        var response = await customer.PostAsJsonAsync("/api/account/closure",
            new { operationId = Guid.NewGuid(), password = registration.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(await world.IsOpenAsync(customerId));
        Assert.Equal(0, await world.ErasedEventCountAsync(customerId));
    }

    [Fact]
    public async Task MalformedOversizedAndMistypedClosureBodiesAreRefusedWithoutSideEffects()
    {
        // Guarantee: hostile bodies get a client error, never a server error, and never close the account.
        using var factory = fixture.CreateFactory();
        using var world = new AdversarialWorld(factory);
        using var customer = factory.CreateClient();
        var registration = await IntakePhotoFixture.RegisterAsync(customer);
        var customerId = await world.UserIdAsync(registration.Email!);

        var malformed = await customer.PostAsync("/api/account/closure", Json("{\"operationId\": \"not-a-guid\", "));
        var oversized = await customer.PostAsync("/api/account/closure", Json(
            "{\"operationId\":\"" + Guid.NewGuid() + "\",\"password\":\"" + new string('p', 200_000) + "\"}"));
        var empty = await customer.PostAsync("/api/account/closure", Json("{}"));
        var wrongType = await customer.PostAsync("/api/account/closure", new StringContent(
            "operationId=" + Guid.NewGuid() + "&password=x", Encoding.UTF8, "application/x-www-form-urlencoded"));

        foreach (var response in new[] { malformed, oversized, empty, wrongType })
        {
            Assert.True((int)response.StatusCode is >= 400 and < 500, ((int)response.StatusCode).ToString());
        }

        Assert.True(await world.IsOpenAsync(customerId));
    }

    [Fact]
    public async Task VisitorsAndCustomersCannotReachAdministratorErasureOrAnotherCustomersAccount()
    {
        // Guarantee: only an Administrator reaches the erasure endpoints, and a customer's own closure never takes an id.
        using var factory = fixture.CreateFactory();
        using var world = new AdversarialWorld(factory);
        using var visitor = factory.CreateClient();
        using var attacker = factory.CreateClient();
        await IntakePhotoFixture.RegisterAsync(attacker);
        var (victim, victimRegistration) = await world.CustomerAsync();
        var body = new { operationId = Guid.NewGuid(), reason = "Subject request", password = victimRegistration.Password };

        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.GetAsync("/api/account/closure/blockers")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.PostAsJsonAsync($"/api/accounts/{victim}/erasures", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await attacker.GetAsync($"/api/accounts/{victim}/erasure-blockers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await attacker.PostAsJsonAsync($"/api/accounts/{victim}/erasures", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await attacker.PostAsJsonAsync($"/api/staff-accounts/{victim}/erasures", body)).StatusCode);
        Assert.True(await world.IsOpenAsync(victim));
        Assert.Equal(0, await world.ErasedEventCountAsync(victim));
    }

    [Fact]
    public async Task AForeignOriginNeverReceivesCrossOriginPermissionForErasure()
    {
        // Guarantee: the API grants cross-origin access only to the configured frontend, so another site's page cannot
        // read an erasure response or preflight a closure.
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/account/closure");
        preflight.Headers.Add("Origin", "https://evil.example");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        var answer = await client.SendAsync(preflight);
        Assert.False(answer.Headers.Contains("Access-Control-Allow-Origin"));

        using var simple = new HttpRequestMessage(HttpMethod.Get, "/api/account/closure/blockers");
        simple.Headers.Add("Origin", "https://evil.example");
        Assert.False((await client.SendAsync(simple)).Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task UnknownFieldsHugeReasonsAndBadRoutesNeverEraseAnything()
    {
        // Guarantee: an Administrator's malformed erasure is a client error that leaves the customer open.
        using var factory = fixture.CreateFactory();
        using var world = new AdversarialWorld(factory);
        using var admin = factory.CreateClient();
        await StaffInvitationTests.SignInAdministratorAsync(factory, admin);
        var (victim, _) = await world.CustomerAsync();

        var hugeReason = await admin.PostAsJsonAsync($"/api/accounts/{victim}/erasures",
            new { operationId = Guid.NewGuid(), reason = new string('r', 5000), password = AdversarialWorld.AdministratorPassword });
        var notAGuid = await admin.PostAsJsonAsync("/api/accounts/not-a-guid/erasures",
            new { operationId = Guid.NewGuid(), reason = "x", password = "y" });
        var nullBody = await admin.PostAsync($"/api/accounts/{victim}/erasures", Json("null"));
        Assert.Equal(HttpStatusCode.BadRequest, hugeReason.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, notAGuid.StatusCode);
        Assert.True((int)nullBody.StatusCode is >= 400 and < 500, ((int)nullBody.StatusCode).ToString());
        Assert.True(await world.IsOpenAsync(victim));
    }

    private static object Reason() => new { operationId = Guid.NewGuid(), reason = "Support check" };

    private static StringContent Json(string body) => new(body, new MediaTypeHeaderValue("application/json"));

    private static async Task<int> EventCountAsync(AdversarialWorld world, Guid targetId) =>
        await world.RunAsync(services => services.GetRequiredService<DealershipDbContext>().AccountEvents.AsNoTracking()
            .CountAsync(item => item.TargetId == targetId));

    private static async Task<(Guid Id, Guid BranchId)> ProvisionAgentAsync(AdversarialWorld world,
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory)
    {
        var branchId = await world.RunAsync(async services => await services.GetRequiredService<DealershipDbContext>()
            .Branches.AsNoTracking().Select(item => item.Id).FirstAsync());
        var email = $"agent.{Guid.NewGuid():N}@example.invalid";
        await world.RunAsync(async services =>
        {
            Assert.True((await services.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(
                new(Guid.NewGuid(), email, AdversarialWorld.AdministratorPassword, AccountRoles.SalesAgent, branchId,
                    "Hana Ismail", "0123456781"), CancellationToken.None)).Created);
            return true;
        });
        return (await world.UserIdAsync(email), branchId);
    }
}
