using System.Globalization;
using System.Data.Common;
using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Application.Intake;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class StaffWorklistTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    private static readonly int[] PageSizes = [25, 16];

    [Fact]
    public async Task CanonicalPopulatedWorklistPreservesScopedPagingStatusesAndCurrentRoles()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<DealershipDbContext>();
        var otherBranch = Guid.NewGuid();
        await services.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
            DevelopmentFixtureManifest.Branches[0] with { Id = otherBranch, Name = "ApexAuto Petaling Jaya" }, CancellationToken.None);
        var manager = Guid.NewGuid();
        await services.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(new StaffProvisioningRequest(
            manager, $"farah.aziz.{Guid.NewGuid():N}@example.invalid", "Password123!", AccountRoles.GeneralManager, null, "Farah Aziz", "0123456708"),
            CancellationToken.None);
        var booked = new List<AppointmentReceipt>();

        for (var index = 0; index < 43; index++)
        {
            booked.Add(await data.BookAsync(index % 6, $"VCA {6200 + index}", index / 6));
        }

        var other = await data.BookAsync(0, "BPL 2198", branchId: otherBranch);
        await data.ClaimAsync(booked[0].Id);
        Assert.Equal(CheckInStatus.Succeeded,
            (await services.GetRequiredService<AppointmentCheckInService>()
                .CheckInAsync(data.StaffId, booked[0].Id, CheckInTestData.Evidence, CancellationToken.None)).Status);
        Assert.Equal(CancellationStatus.Succeeded,
            (await services.GetRequiredService<AppointmentCancellationService>()
                .CancelAsync(data.OwnerId, booked[6].Id, CancellationToken.None)).Status);
        var from = booked[0].Slot.StartsAt.AddHours(8).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var to = booked[^1].Slot.StartsAt.AddHours(8).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var query = StaffWorklistQuery.Parse(from, to, null, null, 1, 25)!;
        var pages = new List<StaffWorklistPage>();

        for (var page = 1; page <= PageSizes.Length; page++)
        {
            pages.Add((await ListAsync(data, data.StaffId, query with { Page = page }))!);
        }

        Assert.Equal(PageSizes, pages.Select(page => page.Items.Count));
        Assert.All(pages, page => Assert.Equal(41, page.Total));
        var expected = booked.Where(item => item.Id != booked[0].Id && item.Id != booked[6].Id)
            .OrderBy(item => item.Slot.StartsAt).ThenBy(item => item.Id).Select(item => item.Id);
        Assert.Equal(expected, pages.SelectMany(page => page.Items).Select(item => item.Id));
        var beyondFirstPage = (await ListAsync(data, data.StaffId, query with { Search = "vca 6242" }))!;
        Assert.Equal(1, beyondFirstPage.Total);
        Assert.Equal(booked[^1].Id, Assert.Single(beyondFirstPage.Items).Id);
        Assert.Equal(41, (await ListAsync(data, data.StaffId, query with { Search = "nUr AiNa" }))!.Total);
        var vehicle = pages[0].Items[0];
        var columnQuery = query with
        {
            Registration = "6242",
            Vehicle = $"{vehicle.ManufactureYear} {vehicle.Make} {vehicle.Model}",
            Customer = "nUr AiNa"
        };
        var columnPage = (await ListAsync(data, data.StaffId, columnQuery))!;
        Assert.Equal(1, columnPage.Total);
        Assert.Equal(booked[^1].Id, Assert.Single(columnPage.Items).Id);
        var columnExport = (await services.GetRequiredService<IStaffWorklistStore>()
            .ExportAsync(data.StaffId, columnQuery, CancellationToken.None))!;
        Assert.Equal(columnPage.Items, columnExport.Items);
        Assert.Equal(0, (await ListAsync(data, data.StaffId, columnQuery with { Customer = "%" }))!.Total);
        Assert.Equal(0, (await ListAsync(data, data.StaffId, columnQuery with { Registration = "BPL" }))!.Total);
        Assert.Equal(0, (await ListAsync(data, data.StaffId, query with { Search = "BPL 2198" }))!.Total);
        Assert.Equal(0, (await ListAsync(data, data.StaffId, query with { Search = "%" }))!.Total);
        Assert.Equal(0, (await ListAsync(data, data.StaffId, query with { Search = "_" }))!.Total);
        // Identifiers are never matched, while the customer's email and mobile are.
        Assert.Equal(0, (await ListAsync(data, data.StaffId, query with { Search = booked[^1].Id.ToString() }))!.Total);
        Assert.Equal(41, (await ListAsync(data, data.StaffId, query with { Search = data.CustomerEmail }))!.Total);
        var descending = (await ListAsync(data, data.StaffId,
            query with { Sort = "registrationPlate", Direction = "desc" }))!;
        Assert.Equal("VCA 6242", descending.Items[0].RegistrationPlate);
        Assert.Equal(41, descending.Total);
        var export = (await services.GetRequiredService<IStaffWorklistStore>().ExportAsync(data.StaffId,
            query with { Page = 3, PageSize = 10, Sort = "registrationPlate", Direction = "desc" }, CancellationToken.None))!;
        Assert.Equal(41, export.Items.Count);
        Assert.Equal(descending.Items.Select(item => item.Id), export.Items.Take(25).Select(item => item.Id));
        Assert.DoesNotContain(export.Items, item => item.Id == other.Id);
        var actor = new CurrentActor(data.StaffId, AccountRoles.SalesAgent, data.BranchId);
        await using (var csv = new MemoryStream())
        {
            Assert.True(await services.GetRequiredService<IStaffExportWriter>().WriteAsync(actor,
                query with { Page = 3, PageSize = 10, Sort = "registrationPlate", Direction = "desc" }, csv, CancellationToken.None));
            Assert.Equal(StaffIntakeCsv.Header + string.Concat(export.Items.Select(StaffIntakeCsv.Row)),
                System.Text.Encoding.UTF8.GetString(csv.ToArray()));
        }
        await using (var deniedCsv = new MemoryStream())
        {
            var writer = services.GetRequiredService<IStaffExportWriter>();
            Assert.False(await writer.WriteAsync(actor with { BranchId = otherBranch }, query, deniedCsv, CancellationToken.None));
            Assert.False(await writer.WriteAsync(actor, query with { BranchId = otherBranch }, deniedCsv, CancellationToken.None));
            Assert.Equal(0, deniedCsv.Length);
        }
        Assert.Null(await services.GetRequiredService<IStaffWorklistStore>().ExportAsync(data.StaffId,
            query with { BranchId = otherBranch }, CancellationToken.None));
        Assert.All(pages.SelectMany(page => page.Items), item =>
        {
            Assert.Equal(data.BranchId, item.BranchId);
            Assert.Equal("Nur Aina Abdullah", item.CustomerName);
            Assert.Equal("Booked", item.Status);
        });
        Assert.Empty((await ListAsync(data, data.StaffId, query with { Page = int.MaxValue }))!.Items);
        Assert.Equal(41, (await ListAsync(data, data.StaffId, query with { Page = int.MaxValue }))!.Total);
        Assert.Null(await ListAsync(data, data.StaffId, query with { BranchId = otherBranch }));
        Assert.Equal(43, (await ListAsync(data, data.StaffId, query with { Status = StaffWorklistStatus.All }))!.Total);
        Assert.Equal(booked[0].Id, Assert.Single((await ListAsync(data, data.StaffId,
            query with { Status = StaffWorklistStatus.CheckedIn }))!.Items).Id);
        Assert.Equal(booked[6].Id, Assert.Single((await ListAsync(data, data.StaffId,
            query with { Status = StaffWorklistStatus.Cancelled }))!.Items).Id);
        var unrestricted = StaffWorklistQuery.Parse(null, null, "All", null, 1, 25)!;
        var secondHandler = Guid.NewGuid();
        var secondEmail = $"zulaikha.hassan.{Guid.NewGuid():N}@example.invalid";
        await services.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(new StaffProvisioningRequest(
            secondHandler, secondEmail, "Password123!", AccountRoles.SalesAgent, data.BranchId, "Nurul Huda", "0123456709"),
            CancellationToken.None);
        await data.ClaimAsync(booked[^1].Id, secondHandler);
        var handlerOrder = unrestricted with { Handler = "@example.invalid", Sort = "handlerEmail", Direction = "desc" };
        var sortedHandlers = (await ListAsync(data, manager, handlerOrder))!;
        Assert.Equal([secondEmail, data.StaffEmail], sortedHandlers.Items.Select(item => item.HandlerEmail));
        Assert.Equal([data.StaffEmail, secondEmail], (await ListAsync(data, manager,
            handlerOrder with { Direction = "asc" }))!.Items.Select(item => item.HandlerEmail));
        var handlerQuery = unrestricted with { Handler = data.StaffEmail.ToUpperInvariant(), Sort = "handlerEmail" };
        var handled = (await ListAsync(data, manager, handlerQuery))!;
        Assert.Equal(booked[0].Id, Assert.Single(handled.Items).Id);
        Assert.Equal(1, handled.Total);
        Assert.Equal(data.StaffEmail, handled.Items[0].HandlerEmail);
        var handledExport = (await services.GetRequiredService<IStaffWorklistStore>()
            .ExportAsync(manager, handlerQuery, CancellationToken.None))!;
        Assert.Equal(handled.Items, handledExport.Items);
        Assert.Equal(0, (await ListAsync(data, manager, handlerQuery with { Handler = "%" }))!.Total);
        await using (var handlerCsv = new MemoryStream())
        {
            var groupActor = new CurrentActor(manager, AccountRoles.GeneralManager, null);
            Assert.True(await services.GetRequiredService<IStaffExportWriter>()
                .WriteAsync(groupActor, handlerQuery, handlerCsv, CancellationToken.None));
            Assert.Equal(StaffIntakeCsv.Header + StaffIntakeCsv.Row(handled.Items[0]),
                System.Text.Encoding.UTF8.GetString(handlerCsv.ToArray()));
        }
        Assert.Equal(43, (await ListAsync(data, data.StaffId, unrestricted))!.Total);
        Assert.Equal(44, (await ListAsync(data, manager, unrestricted))!.Total);
        Assert.Equal(43, (await services.GetRequiredService<IStaffWorklistStore>()
            .ExportAsync(data.StaffId, unrestricted, CancellationToken.None))!.Items.Count);
        var today = StaffWorklistQuery.Parse(from, from, null, null, 1, 25)!;
        Assert.Equal(5, (await ListAsync(data, data.StaffId, today))!.Total);
        Assert.Equal(6, (await ListAsync(data, data.StaffId, today with
        {
            From = today.Until,
            Until = today.Until!.Value.AddDays(1),
            Status = StaffWorklistStatus.All
        }))!.Total);

        var allBranches = (await ListAsync(data, manager, query with { Status = StaffWorklistStatus.All, PageSize = 50 }))!;
        Assert.Equal(44, allBranches.Total);
        Assert.Equal(other.Id, Assert.Single((await ListAsync(data, manager,
            query with { Search = "pEtAlInG" }))!.Items).Id);
        var statusOrder = (await ListAsync(data, manager,
            query with { Status = StaffWorklistStatus.All, Sort = "status", Direction = "desc", PageSize = 50 }))!;
        Assert.Equal("CheckedIn", statusOrder.Items[0].Status);
        Assert.Equal("Cancelled", statusOrder.Items[1].Status);
        Assert.Equal(allBranches.Items.OrderBy(item => item.StartsAt).ThenBy(item => item.Id).Select(item => item.Id),
            allBranches.Items.Select(item => item.Id));
        var tie = allBranches.Items.Where(item => item.StartsAt == other.Slot.StartsAt).ToArray();
        Assert.Equal(2, tie.Length);
        Assert.Equal(tie.Select(item => item.Id).Order(), tie.Select(item => item.Id));
        Assert.Equal(other.Id, Assert.Single((await ListAsync(data, manager,
            query with { BranchId = otherBranch }))!.Items).Id);
        Assert.Equal(0, (await ListAsync(data, manager, query with { BranchId = Guid.NewGuid() }))!.Total);
        await AssertCountAndRowsShareSnapshotAsync(data, query, booked[7].Id);

        foreach (var role in new[] { AccountRoles.Customer, AccountRoles.WorkshopTechnician, AccountRoles.Administrator })
        {
            var branchId = role == AccountRoles.WorkshopTechnician ? (Guid?)data.BranchId : null;
            await database.Users.Where(user => user.Id == data.StaffId).ExecuteUpdateAsync(update => update
                .SetProperty(user => user.Role, role).SetProperty(user => user.BranchId, branchId));
            Assert.Null(await ListAsync(data, data.StaffId, query));
            Assert.Null(await services.GetRequiredService<IStaffWorklistStore>().ExportAsync(data.StaffId, query, CancellationToken.None));
        }

        await database.Users.Where(user => user.Id == data.StaffId).ExecuteUpdateAsync(update => update
            .SetProperty(user => user.Role, AccountRoles.BranchManager).SetProperty(user => user.BranchId, otherBranch));
        var historicalHandler = Assert.Single((await ListAsync(data, manager, handlerQuery))!.Items);
        Assert.Equal(data.StaffEmail, historicalHandler.HandlerEmail);
        Assert.False(historicalHandler.HandlerEligible);
        Assert.Equal(other.Id, Assert.Single((await ListAsync(data, data.StaffId, query))!.Items).Id);
        await database.Branches.Where(branch => branch.Id == otherBranch)
            .ExecuteUpdateAsync(update => update.SetProperty(branch => branch.IsActive, false));
        Assert.Null(await ListAsync(data, data.StaffId, query));
        Assert.Equal(44, (await ListAsync(data, manager, query with { Status = StaffWorklistStatus.All }))!.Total);
        await database.Users.Where(user => user.Id == manager)
            .ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, false));
        Assert.Null(await ListAsync(data, manager, query));
    }

    private static async Task<StaffWorklistPage?> ListAsync(CheckInTestData data, Guid staffId, StaffWorklistQuery query)
    {
        using var scope = data.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IStaffWorklistStore>()
            .ListAsync(staffId, query, CancellationToken.None);
    }

    private async Task AssertCountAndRowsShareSnapshotAsync(CheckInTestData data, StaffWorklistQuery query, Guid appointmentId)
    {
        var interceptor = new AfterCountInterceptor(async () =>
        {
            using var scope = data.Factory.Services.CreateScope();
            Assert.Equal(CancellationStatus.Succeeded,
                (await scope.ServiceProvider.GetRequiredService<AppointmentCancellationService>()
                    .CancelAsync(data.OwnerId, appointmentId, CancellationToken.None)).Status);
        });
        var options = new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.RuntimeConnectionString).AddInterceptors(interceptor).Options;
        await using var database = new DealershipDbContext(options);
        var snapshot = (await new StaffWorklistStore(database)
            .ListAsync(data.StaffId, query with { PageSize = 50 }, CancellationToken.None))!;
        Assert.True(interceptor.Invoked);
        Assert.Equal(41, snapshot.Total);
        Assert.Equal(41, snapshot.Items.Count);
        Assert.Equal("Booked", Assert.Single(snapshot.Items, item => item.Id == appointmentId).Status);
        Assert.Equal(40, (await ListAsync(data, data.StaffId, query))!.Total);
    }

    private sealed class AfterCountInterceptor(Func<Task> afterCount) : DbCommandInterceptor
    {
        public bool Invoked { get; private set; }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
            CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (!Invoked && command.CommandText.Contains("SELECT count(*)", StringComparison.Ordinal) &&
                command.CommandText.Contains("IntakeAppointments", StringComparison.Ordinal))
            {
                Invoked = true;
                await afterCount();
            }

            return result;
        }
    }
}
