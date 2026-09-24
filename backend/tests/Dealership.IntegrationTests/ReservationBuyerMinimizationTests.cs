using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Dealership.IntegrationTests.ReservationTestData;
using static Dealership.IntegrationTests.RetentionSql;

namespace Dealership.IntegrationTests;

public sealed class ReservationBuyerMinimizationTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task OnlyAClosedCustomersInactiveReservationCanLoseItsContactCopies()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var manager = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var technician = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var listing = await CreateListingAsync(data, services, manager, technician, 0);
        await using var database = NewContext();
        database.PurchaseSettings.Add(new(500m, 0.08m, manager, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        var request = new ReservationRequest(Guid.NewGuid(), listing.Publication.Id, null);
        await using (var command = NewContext())
        {
            var service = new ReservationService(new ReservationStore(command, new MailDeliverySignal(),
                new ReservationExpirySignal()));
            Assert.Equal(ReservationStatus.Succeeded, (await service.ReserveAsync(data.OwnerId,
                listing.Revision.VehicleId, request, CancellationToken.None)).Status);
        }

        const string removeContact = "UPDATE \"Reservations\" SET \"BuyerEmail\" = NULL, \"BuyerMobile\" = NULL, " +
            "\"BuyerNric\" = NULL WHERE \"Id\" = {0}";
        await ExpectCheckViolationAsync(database, removeContact, request.OperationId);

        Assert.Equal(ReservationStatus.Succeeded, (await services.GetRequiredService<ReservationCancellationService>()
            .CancelAsync(data.OwnerId, request.OperationId, new(Guid.NewGuid(), null), CancellationToken.None)).Status);
        // A finished reservation may also lose its copies to the retention executor, so the removal statement is allowed.

        await database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "AspNetUsers" SET "ClosedAt" = now(), "IsActive" = false, "PasswordHash" = NULL,
                "EmailConfirmed" = false, "Email" = NULL, "NormalizedEmail" = NULL, "AccountUsername" = NULL,
                "UserName" = NULL, "NormalizedUserName" = NULL
            WHERE "Id" = {data.OwnerId}
            """);
        await ExpectCheckViolationAsync(database,
            "UPDATE \"Reservations\" SET \"BuyerName\" = NULL WHERE \"Id\" = {0}", request.OperationId);
        await ExpectCheckViolationAsync(database,
            "UPDATE \"Reservations\" SET \"BuyerEmail\" = NULL, \"Number\" = 'VRS-2026-000999' WHERE \"Id\" = {0}",
            request.OperationId);

        Assert.Equal(1, await database.Database.ExecuteSqlRawAsync(removeContact, request.OperationId));
        var stored = await database.Reservations.AsNoTracking().SingleAsync(item => item.Id == request.OperationId);
        Assert.Null(stored.Buyer.Email);
        Assert.Null(stored.Buyer.Mobile);
        Assert.Null(stored.Buyer.Nric);
        Assert.False(string.IsNullOrWhiteSpace(stored.Buyer.LegalName));
    }

    private DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
        .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);

    private static Task ExpectCheckViolationAsync(DealershipDbContext database, string sql, Guid id) =>
        ExpectFailureAsync(database, PostgresErrorCodes.CheckViolation, sql, id);
}
