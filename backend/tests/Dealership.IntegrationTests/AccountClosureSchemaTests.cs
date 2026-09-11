using System.Net;
using System.Net.Http.Json;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class AccountClosureSchemaTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    private const string CloseUser = """
        UPDATE "AspNetUsers" SET "ClosedAt" = now(), "IsActive" = false, "SuspendedAt" = NULL, "PasswordHash" = NULL,
            "EmailConfirmed" = false, "Email" = NULL, "NormalizedEmail" = NULL, "UserName" = NULL,
            "NormalizedUserName" = NULL, "AccountUsername" = NULL
        WHERE "Id" = {0}
        """;

    [Fact]
    public async Task AnOpenAccountCannotLoseItsIdentityAndAClosedOneMustLoseAllOfIt()
    {
        using var factory = fixture.CreateFactory();
        using var customer = factory.CreateClient();
        var registration = await IntakePhotoFixture.RegisterAsync(customer);
        var id = await CustomerIdAsync(factory, registration.Email!);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();

        await ExpectCheckFailureAsync(database, "CK_Users_EmailNormalization",
            "UPDATE \"AspNetUsers\" SET \"Email\" = NULL, \"NormalizedEmail\" = NULL WHERE \"Id\" = {0}", id);
        await ExpectCheckFailureAsync(database, "CK_Users_AccountUsername",
            "UPDATE \"AspNetUsers\" SET \"AccountUsername\" = NULL WHERE \"Id\" = {0}", id);
        await ExpectCheckFailureAsync(database, "CK_CustomerProfiles_RequiredText",
            "UPDATE \"CustomerProfiles\" SET \"LegalName\" = NULL WHERE \"UserId\" = {0}", id);
        await ExpectCheckFailureAsync(database, "CK_CustomerProfiles_Nric",
            "UPDATE \"CustomerProfiles\" SET \"Nric\" = NULL WHERE \"UserId\" = {0}", id);
        await ExpectCheckFailureAsync(database, "CK_Users_AccountUsername",
            "UPDATE \"AspNetUsers\" SET \"ClosedAt\" = now() WHERE \"Id\" = {0}", id);
        await ExpectCheckFailureAsync(database, "CK_Users_EmailNormalization",
            "UPDATE \"AspNetUsers\" SET \"ClosedAt\" = now(), \"IsActive\" = false, \"PasswordHash\" = NULL, " +
            "\"EmailConfirmed\" = false, \"UserName\" = NULL, \"NormalizedUserName\" = NULL, \"AccountUsername\" = NULL " +
            "WHERE \"Id\" = {0}", id);
        await ExpectCheckFailureAsync(database, "CK_Users_Closed",
            "UPDATE \"AspNetUsers\" SET \"ClosedAt\" = now(), \"IsActive\" = false, \"EmailConfirmed\" = false, " +
            "\"Email\" = NULL, \"NormalizedEmail\" = NULL, \"UserName\" = NULL, \"NormalizedUserName\" = NULL, " +
            "\"AccountUsername\" = NULL WHERE \"Id\" = {0}", id);

        Assert.Equal(1, await database.Database.ExecuteSqlRawAsync(CloseUser, id));
        Assert.Equal(1, await database.Database.ExecuteSqlRawAsync(
            "UPDATE \"CustomerProfiles\" SET \"ErasedAt\" = now(), \"LegalName\" = NULL, \"Nric\" = NULL, " +
            "\"Mobile\" = NULL, \"ResidentialAddress\" = NULL WHERE \"UserId\" = {0}", id));
    }

    [Fact]
    public async Task AdministratorWritersTreatAClosedCustomerAsGone()
    {
        using var factory = fixture.CreateFactory();
        using var admin = factory.CreateClient();
        await StaffInvitationTests.SignInAdministratorAsync(factory, admin);
        using var customer = factory.CreateClient();
        var registration = await IntakePhotoFixture.RegisterAsync(customer);
        var id = await CustomerIdAsync(factory, registration.Email!);
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            await database.Database.ExecuteSqlRawAsync(CloseUser, id);
            await database.Database.ExecuteSqlRawAsync(
                "UPDATE \"CustomerProfiles\" SET \"ErasedAt\" = now(), \"LegalName\" = NULL, \"Nric\" = NULL, " +
                "\"Mobile\" = NULL, \"ResidentialAddress\" = NULL WHERE \"UserId\" = {0}", id);
        }

        var reason = new { operationId = Guid.NewGuid(), reason = "Support check" };
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync($"/api/accounts/{id}/suspensions",
            new
            {
                operationId = Guid.NewGuid(),
                reason = "Support check",
                requirePasswordReset = false,
                requireEmailVerification = false
            })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync($"/api/accounts/{id}/nric-reveals",
            reason)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync($"/api/accounts/{id}/session-revocations",
            reason)).StatusCode);
        Assert.Equal(0, await EventCountAsync(factory, id));
    }

    private static async Task ExpectCheckFailureAsync(DealershipDbContext database, string constraint, string sql,
        Guid id)
    {
        await using var transaction = await database.Database.BeginTransactionAsync();
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => database.Database.ExecuteSqlRawAsync(sql, id));
        var postgres = Assert.IsType<PostgresException>(failure is DbUpdateException ? failure.InnerException : failure);
        Assert.Equal(PostgresErrorCodes.CheckViolation, postgres.SqlState);
        Assert.Equal(constraint, postgres.ConstraintName);
        await transaction.RollbackAsync();
    }

    private static async Task<int> EventCountAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, Guid targetId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().AccountEvents
            .CountAsync(item => item.TargetId == targetId);
    }

    private static async Task<Guid> CustomerIdAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var normalized = email.ToUpperInvariant();
        return await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().Users
            .Where(user => user.NormalizedEmail == normalized).Select(user => user.Id).SingleAsync();
    }
}
