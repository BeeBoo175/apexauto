using Dealership.Application.Sales;
using Dealership.Application.Accounts;
using Dealership.Application.Common;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Dealership.IntegrationTests;

public sealed class SaleListTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task QueuesAndTablesReadTheSaleHeaderWithSearchAndTheFirstTabWithWork()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        var open = await world.HoldAsync();
        var mine = await world.HoldAsync();
        await world.Handling.ClaimAsync(world.Agent, mine.Id, new(Guid.NewGuid(), 1), default);

        var queue = (await world.Lists.QueueAsync(world.Agent, "ToClaim", null, 1, 25, default)).Value!;
        Assert.Contains(open.Id, queue.Items.Select(item => item.Id));
        Assert.Equal(1, queue.Counts.Handling);
        // A search spans every tab and says where each match sits.
        var search = (await world.Lists.QueueAsync(world.Agent, "ToClaim", mine.Number, 1, 25, default)).Value!;
        Assert.Equal(("Handling", mine.Id), (Assert.Single(search.Items).Status, search.Items[0].Id));
        Assert.Equal(SaleStatus.Forbidden, (await world.Lists.QueueAsync(world.Manager, "ToClaim", null, 1, 25,
            default)).Status);

        // With nothing awaiting approval the table opens on Open, and tab totals ignore the search.
        var table = (await world.Lists.TableAsync(world.Manager,
            new SaleTableQuery(1, 25, null, mine.Buyer.Mobile, null, false), default)).Value!;
        Assert.Equal("Open", table.View);
        Assert.Equal((1, 1), (table.Counts.Open, table.Counts.BeingHandled));
        Assert.Equal(SaleStatus.Forbidden, (await world.Lists.TableAsync(world.Agent,
            new SaleTableQuery(1, 25, null, null, null, false), default)).Status);
    }

    [Fact]
    public async Task ColumnFiltersNarrowTheManagerTable()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        var held = await world.HoldAsync();
        var handling = new SaleTableQuery(1, 25, "BeingHandled", null, null, false);
        await world.Handling.ClaimAsync(world.Agent, held.Id, new(Guid.NewGuid(), 1), default);

        var byReference = (await world.Lists.TableAsync(world.Manager, handling with { Reference = held.Number },
            default)).Value!;
        Assert.Equal(held.Id, Assert.Single(byReference.Items).Id);
        var noPlate = (await world.Lists.TableAsync(world.Manager, handling with
        {
            Reference = held.Number,
            Registration = "NO-SUCH-PLATE"
        }, default)).Value!;
        Assert.Empty(noPlate.Items);
        var discount = (await world.Lists.TableAsync(world.Manager, handling with
        {
            Reference = held.Number,
            MinimumDiscount = 0.01m
        }, default)).Value!;
        Assert.Empty(discount.Items);
        // Close the sale so the shared branch's tab counts stay as the other tests expect.
        Assert.Equal(SaleStatus.Succeeded, (await world.Terms.CancelAsync(world.Manager, held.Id,
            new(Guid.NewGuid(), 2, "Filter test finished"), default)).Status);

        Assert.Null(SaleQueries.Table(1, 25, null, null, null, null, null, null, null, 10m, 5m, null, null,
            null, null, null, null, null, null, null, null));
        Assert.Null(SaleQueries.Table(1, 25, null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, "2026-11-02", "2026-11-01"));
    }

    [Fact]
    public async Task RemainingTimeAndUpdatedFiltersRunBeforeCountingPagingAndSorting()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        await using var database = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
        var holds = new List<Reservation>();
        try
        {
            foreach (var hours in new[] { 12, 36 })
            {
                var held = await world.HoldAsync();
                holds.Add(held);
                // Shift only an owned sale's clock, as in expiry tests; reservation confirmation stays intact.
                await using var maintenance = new NpgsqlConnection(fixture.Database.ConnectionString);
                await maintenance.OpenAsync();
                await using var command = new NpgsqlCommand("""
                    SET session_replication_role = replica;
                    UPDATE "SalesOrders" SET "DueAt" = clock_timestamp() + @hours * interval '1 hour' WHERE "Id" = @id;
                    SET session_replication_role = DEFAULT;
                    """, maintenance);
                command.Parameters.AddWithValue("hours", hours);
                command.Parameters.AddWithValue("id", held.Id);
                await command.ExecuteNonQueryAsync();
            }
            var query = new SaleTableQuery(1, 10, "Open", null, "timeLeft", true) { MaximumHoursLeft = 40m };
            var running = (await world.Lists.TableAsync(world.Manager, query, default)).Value!;
            Assert.Equal(2, running.Total);
            Assert.Equal(holds.Select(item => item.Id).Reverse(), running.Items.Select(item => item.Id));
            var beyond = (await world.Lists.TableAsync(world.Manager, query with { Page = 2 }, default)).Value!;
            Assert.Equal(2, beyond.Total);
            Assert.Empty(beyond.Items);
            var shortHold = (await world.Lists.TableAsync(world.Manager, query with
            {
                MinimumHoursLeft = 6m,
                MaximumHoursLeft = 24m
            }, default)).Value!;
            Assert.Equal((1, holds[0].Id), (shortHold.Total, Assert.Single(shortHold.Items).Id));
            Assert.Equal(running.Counts, shortHold.Counts);

            foreach (var held in holds)
            {
                Assert.Equal(SaleStatus.Succeeded, (await world.Handling.ClaimAsync(world.Agent, held.Id,
                    new(Guid.NewGuid(), 1), default)).Status);
            }
            var pausedQuery = query with { View = "BeingHandled", MinimumHoursLeft = 6m, MaximumHoursLeft = 24m };
            var paused = (await world.Lists.TableAsync(world.Manager, pausedQuery, default)).Value!;
            Assert.Equal((1, holds[0].Id), (paused.Total, Assert.Single(paused.Items).Id));
            var allPaused = (await world.Lists.TableAsync(world.Manager, query with { View = "BeingHandled" }, default)).Value!;
            Assert.Equal(holds.Select(item => item.Id).Reverse(), allPaused.Items.Select(item => item.Id));
            var updatedOrder = (await world.Lists.TableAsync(world.Manager, query with
            {
                View = "BeingHandled",
                Sort = "updated"
            }, default)).Value!;
            Assert.Equal(allPaused.Items.Select(item => item.Id), updatedOrder.Items.Select(item => item.Id));
            var hoursLeft = paused.Items[0].PausedSeconds!.Value / 3600m;
            Assert.Single((await world.Lists.TableAsync(world.Manager, pausedQuery with
            {
                MinimumHoursLeft = hoursLeft - 0.0001m,
                MaximumHoursLeft = hoursLeft + 0.0001m
            }, default)).Value!.Items);
            Assert.Empty((await world.Lists.TableAsync(world.Manager, pausedQuery with
            {
                MaximumHoursLeft = hoursLeft - 0.0001m
            }, default)).Value!.Items);

            var updatedDay = paused.Items[0].UpdatedAt.ToOffset(TimeSpan.FromHours(8))
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var byUpdated = pausedQuery with { Sort = "updated", Updated = ListDateRange.Parse(updatedDay, updatedDay) };
            Assert.Single((await world.Lists.TableAsync(world.Manager, byUpdated, default)).Value!.Items);
            Assert.Empty((await world.Lists.TableAsync(world.Manager, byUpdated with
            {
                Updated = new(DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(2))
            }, default)).Value!.Items);
            await VerifyFilterBindingAsync(world, database, holds[0], updatedDay);
        }
        finally
        {
            foreach (var held in holds)
            {
                var sale = (await world.Reads.GetAsync(world.Manager, held.Id, default)).Value!;
                Assert.Equal(SaleStatus.Succeeded, (await world.Terms.CancelAsync(world.Manager, held.Id,
                    new(Guid.NewGuid(), sale.Sale.Version, "Filter verification finished"), default)).Status);
            }
        }
        var history = (await world.Lists.TableAsync(world.Manager,
            new SaleTableQuery(1, 10, "History", holds[0].Number, "timeLeft", false)
            {
                MinimumHoursLeft = 0m
            }, default)).Value!;
        Assert.Empty(history.Items);
    }

    private async Task VerifyFilterBindingAsync(SaleWorld world, DealershipDbContext database, Reservation held,
        string updatedDay)
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var email = await database.Users.Where(item => item.Id == world.Manager).Select(item => item.Email).SingleAsync();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email!, "Password123!"));
        var session = (await login.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var path = $"/api/staff/sales?view=BeingHandled&reference={held.Number}";
        var result = await client.GetAsync($"{path}&minimumHoursLeft=6&maximumHoursLeft=24&sort=updated&direction=desc" +
            $"&updatedFrom={updatedDay}&updatedTo={updatedDay}");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(held.Id, Assert.Single((await result.Content.ReadFromJsonAsync<SaleTablePage>())!.Items).Id);
        foreach (var invalid in new[] { "minimumHoursLeft=-1", "maximumHoursLeft=169",
            "minimumHoursLeft=24&maximumHoursLeft=6", "sort=claimed", "updatedFrom=2026-11-02&updatedTo=2026-11-01" })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{path}&{invalid}")).StatusCode);
        }
    }
}
