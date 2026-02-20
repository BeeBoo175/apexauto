using Dealership.Application.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.BrowserJourneys;

internal sealed record RecoveryBrowserAccount(Guid UserId, string Email, string LegalName,
    string[] OldSessions, string? UnrelatedSession);

internal static class RecoveryBrowserFixture
{
    public const string InitialPassword = "Password123!";
    public const string RecoveredPassword = "Recovered123!";

    public static async Task<RecoveryBrowserAccount> PrepareAsync(IServiceProvider services, string scenario)
    {
        using var scope = services.CreateScope();

        if (scenario == "request")
        {
            return await ProvisionDisabledStaffAsync(scope.ServiceProvider);
        }

        var owner = await RegisterAsync(scope.ServiceProvider, "Nur Aina Abdullah");
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var userId = await database.Users.Where(user => user.Email == owner.Email).Select(user => user.Id).SingleAsync();
        var sessions = new List<string>();
        string? unrelatedSession = null;

        if (scenario == "reset")
        {
            sessions.Add(await LoginAsync(scope.ServiceProvider, owner));
            sessions.Add(await LoginAsync(scope.ServiceProvider, owner));
            var other = await RegisterAsync(scope.ServiceProvider, "Tan Mei Ling");
            unrelatedSession = await LoginAsync(scope.ServiceProvider, other);
        }

        await scope.ServiceProvider.GetRequiredService<PasswordRecoveryService>()
            .RequestAsync(new PasswordRecoveryRequest(owner.Email), CancellationToken.None);

        return new RecoveryBrowserAccount(userId, owner.Email!, owner.LegalName!, [.. sessions], unrelatedSession);
    }

    private static async Task<RecoveryBrowserAccount> ProvisionDisabledStaffAsync(IServiceProvider services)
    {
        var id = Guid.NewGuid();
        var email = $"recovery.staff.{Guid.NewGuid():N}@example.invalid";
        var request = new StaffProvisioningRequest(id, email, InitialPassword, AccountRoles.Administrator, null);
        var provisioned = await services.GetRequiredService<StaffProvisioningService>()
            .ProvisionStaffAsync(request, CancellationToken.None);

        if (!provisioned.Created || !provisioned.MatchesRequestedState)
        {
            throw new InvalidOperationException("Canonical recovery staff provisioning failed.");
        }

        var database = services.GetRequiredService<DealershipDbContext>();
        await database.Users.Where(user => user.Id == id).ExecuteUpdateAsync(update =>
            update.SetProperty(user => user.IsActive, false).SetProperty(user => user.EmailConfirmed, false));
        return new RecoveryBrowserAccount(id, email, "System administrator", [], null);
    }

    private static async Task<RegistrationRequest> RegisterAsync(IServiceProvider services, string legalName)
    {
        var request = new RegistrationRequest(Guid.NewGuid(), legalName,
            $"recovery.{Guid.NewGuid():N}@example.invalid", InitialPassword,
            $"940215{Random.Shared.Next(100000, 1000000)}", "+60123456789",
            "18 Jalan Melati, Petaling Jaya, Selangor");
        var registered = await services.GetRequiredService<RegistrationService>()
            .RegisterAsync(request, CancellationToken.None);

        if (registered.Status != RegistrationStatus.Succeeded)
        {
            throw new InvalidOperationException("Canonical recovery customer registration failed.");
        }

        return request;
    }

    private static async Task<string> LoginAsync(IServiceProvider services, RegistrationRequest account)
    {
        var session = await services.GetRequiredService<AuthenticationService>()
            .LoginAsync(new LoginRequest(Guid.NewGuid(), account.Email, InitialPassword), CancellationToken.None);
        return session?.AccessToken ?? throw new InvalidOperationException("Canonical recovery fixture login failed.");
    }
}
