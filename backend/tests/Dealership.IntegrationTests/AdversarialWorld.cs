using Dealership.Application.Accounts;
using Dealership.Domain.Accounts;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

// A disposable host over the restricted runtime role for scenarios that drive the services directly, so a scenario is
// not limited by the five-per-minute credential budget that guards the HTTP endpoints.
internal sealed class AdversarialWorld(WebApplicationFactory<Program> factory) : IDisposable
{
    public const string AdministratorPassword = "Password123!";

    public WebApplicationFactory<Program> Factory { get; } = factory;

    public static AdversarialWorld Create(IntakePhotoFixture fixture) => new(fixture.CreateFactory());

    public async Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        return await action(scope.ServiceProvider);
    }

    public Task<AccountErasureResult> EraseCustomerAsync(Guid administratorId, Guid customerId, Guid operationId,
        string? password, string? reason = "Subject request") =>
        RunAsync(services => services.GetRequiredService<AccountErasureService>().EraseCustomerAsync(administratorId,
            customerId, new(operationId, reason, password), CancellationToken.None));

    public Task<AccountErasureResult> EraseStaffAsync(Guid administratorId, Guid staffId, Guid operationId,
        string? password, string? reason = "Left the company") =>
        RunAsync(services => services.GetRequiredService<AccountErasureService>().EraseStaffAsync(administratorId,
            staffId, new(operationId, reason, password), CancellationToken.None));

    public Task<CustomerClosureResult> CloseAsync(Guid customerId, Guid operationId, string? password) =>
        RunAsync(services => services.GetRequiredService<CustomerClosureService>().CloseAsync(customerId,
            new(operationId, password), CancellationToken.None));

    public async Task<Guid> AdministratorAsync()
    {
        var email = $"admin.{Guid.NewGuid():N}@example.invalid";
        await RunAsync(async services =>
        {
            var created = await services.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(
                new(Guid.NewGuid(), email, AdministratorPassword, AccountRoles.Administrator, null, "Amir Hakim",
                    "0123456789"), CancellationToken.None);
            Assert.True(created.Created);
            return true;
        });
        return await UserIdAsync(email);
    }

    public async Task<(Guid Id, RegistrationRequest Registration)> CustomerAsync(RegistrationRequest? reuse = null)
    {
        var registration = reuse ?? new RegistrationRequest(Guid.NewGuid(), "Nur Aina Abdullah",
            $"nur.aina.{Guid.NewGuid():N}@example.invalid", "Customer-Pass9!", $"940215{Random.Shared.Next(100000, 1000000)}",
            "0123456789", "18 Jalan Melati, Petaling Jaya, Selangor", TestUsernames.Next());
        var result = await RunAsync(services => services.GetRequiredService<RegistrationService>()
            .RegisterAsync(registration, CancellationToken.None));
        Assert.Equal(RegistrationStatus.Succeeded, result.Status);
        return (await UserIdAsync(registration.Email!), registration);
    }

    public Task<Guid> UserIdAsync(string email) => RunAsync(services =>
    {
        var normalized = email.ToUpperInvariant();
        return services.GetRequiredService<DealershipDbContext>().Users.AsNoTracking()
            .Where(user => user.NormalizedEmail == normalized).Select(user => user.Id).SingleAsync();
    });

    public Task<AccountUser> UserAsync(Guid id) => RunAsync(services =>
        services.GetRequiredService<DealershipDbContext>().Users.AsNoTracking().SingleAsync(user => user.Id == id));

    public Task<bool> IsOpenAsync(Guid id) => RunAsync(services =>
        services.GetRequiredService<DealershipDbContext>().Users.AsNoTracking()
            .AnyAsync(user => user.Id == id && user.ClosedAt == null));

    public Task<int> ErasedEventCountAsync(Guid targetId) => RunAsync(services =>
        services.GetRequiredService<DealershipDbContext>().AccountEvents.AsNoTracking()
            .CountAsync(item => item.TargetId == targetId && item.Kind == AccountEventKind.Erased));

    public void Dispose() => Factory.Dispose();
}
