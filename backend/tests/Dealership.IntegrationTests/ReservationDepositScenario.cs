using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

internal sealed class ReservationDepositScenario : IDisposable
{
    private readonly IServiceScope _scope;
    private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory;

    private ReservationDepositScenario(CheckInTestData data, IServiceScope scope, DealershipDbContext database,
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, ListingRecord listing, Guid managerId)
    {
        Data = data;
        ManagerId = managerId;
        VehicleId = listing.Revision.VehicleId;
        _scope = scope;
        Database = database;
        _factory = factory;
        Client = factory.CreateClient();
        Request = new ReservationRequest(Guid.NewGuid(), listing.Publication.Id, null);
        RequestPath = $"/api/vehicles/{listing.Revision.VehicleId}/reservation-requests";
        ReservePath = $"/api/vehicles/{listing.Revision.VehicleId}/reservations";
        Tokens = factory.Services.GetRequiredService<ReservationDepositToken>();
    }

    public CheckInTestData Data { get; }
    public Guid ManagerId { get; }
    public Guid VehicleId { get; }
    public DealershipDbContext Database { get; }
    public HttpClient Client { get; }
    public ReservationRequest Request { get; }
    public string RequestPath { get; }
    public string ReservePath { get; }
    public ReservationDepositToken Tokens { get; }

    // For scenarios that replace a service, such as the payment gateway, on the same database and clock.
    public Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory => _factory;

    public static async Task<ReservationDepositScenario> CreateAsync(IntakePhotoFixture fixture)
    {
        var data = await CheckInTestData.CreateAsync(fixture);
        var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var manager = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var technician = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var listing = await CreateListingAsync(data, services, manager, technician, 0);
        var database = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
        database.PurchaseSettings.Add(new(500m, 0.08m, manager, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        // A separate factory keeps the real clock, so the issued tokens and the 24 hour window are valid.
        var factory = fixture.CreateFactory();
        return new ReservationDepositScenario(data, scope, database, factory, listing, manager);
    }

    public async Task<string> ProvisionOtherBranchStaffEmailAsync()
    {
        var services = _scope.ServiceProvider;
        var otherBranch = Guid.NewGuid();
        await services.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
            DevelopmentFixtureManifest.Branches[1] with { Id = otherBranch }, CancellationToken.None);
        var staff = await ProvisionAsync(services, otherBranch, AccountRoles.SalesAgent);
        return (await Database.Users.Where(item => item.Id == staff).Select(item => item.Email).SingleAsync())!;
    }

    public Task<Guid> RegisterOtherCustomerAsync()
    {
        return RegisterCustomerAsync(_scope.ServiceProvider, Database);
    }

    public void Dispose()
    {
        Client.Dispose();
        _factory.Dispose();
        Database.Dispose();
        _scope.Dispose();
        Data.Dispose();
    }
}
