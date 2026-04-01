using System.Net;
using System.Net.Http.Json;
using Dealership.Application.Vehicles;

namespace Dealership.IntegrationTests;

internal static class ReservationSortingAssertions
{
    internal static async Task VerifyAsync(IReservationReadStore reads, Guid manager,
        IReadOnlyList<ReservationHistoryItem> all)
    {
        string[] sorts = ["reference", "vehicle", "buyer", "branch", "created", "expires", "total", "status"];
        foreach (var sort in sorts)
        {
            Assert.True(all.Select(Key(sort)).Distinct().Count() > 1, $"The fixture must vary {sort} to prove sorting.");
            await VerifyOrderAsync(reads, manager, all, new(PageSize: 50, Sort: sort));
            await VerifyOrderAsync(reads, manager, all, new(PageSize: 50, Sort: sort, Direction: "desc"));
        }

        // Filter membership and the full export traversal must use the same order as individual pages.
        await VerifyOrderAsync(reads, manager, all, new(PageSize: 10, IsActive: true, Sort: "expires"));
        await VerifyOrderAsync(reads, manager, all, new(PageSize: 50, IsActive: false, Sort: "total", Direction: "desc"));
        await VerifyOrderAsync(reads, manager, all, new(all[0].Receipt.BranchId, PageSize: 25, Sort: "vehicle"));
    }

    internal static async Task VerifyHttpAsync(HttpClient client, Guid[] expected)
    {
        using var response = await client.GetAsync("/api/staff/reservations?sort=reference&direction=desc&pageSize=10&page=2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var page = (await response.Content.ReadFromJsonAsync<ReservationHistoryPage>())!;
        Assert.Equal(expected.Length, page.Total);
        Assert.Equal(expected.OrderDescending().Skip(10).Take(10), page.Items.Select(item => item.Receipt.Id));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/staff/reservations?sort=unknown")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/staff/reservations?sort=total&direction=sideways")).StatusCode);
    }

    private static async Task VerifyOrderAsync(IReservationReadStore reads, Guid manager,
        IReadOnlyList<ReservationHistoryItem> all, ReservationReadFilter filter)
    {
        var matching = all.Where(item => (filter.BranchId == null || item.Receipt.BranchId == filter.BranchId) &&
            (filter.IsActive == null || item.IsActive == filter.IsActive)).ToArray();
        var key = Key(filter.Sort);
        var ordered = filter.Direction == "desc" ? matching.OrderByDescending(key) : matching.OrderBy(key);
        var expected = ordered.ThenBy(item => item.Receipt.Id).Select(item => item.Receipt.Id).ToArray();
        var actual = new List<Guid>();
        var pages = Math.Max(1, (matching.Length + filter.PageSize - 1) / filter.PageSize);
        for (var page = 1; page <= pages; page++)
        {
            var result = await reads.StaffAsync(manager, filter with { Page = page }, CancellationToken.None);
            Assert.Equal(ReservationStatus.Succeeded, result.Status);
            Assert.Equal(matching.Length, result.History!.Total);
            Assert.Equal(Math.Min(filter.PageSize, matching.Length - actual.Count), result.History.Items.Count);
            actual.AddRange(result.History.Items.Select(item => item.Receipt.Id));
        }

        Assert.True(expected.SequenceEqual(actual), $"Unexpected {filter.Sort} {filter.Direction} order across pages.");
        Assert.Empty((await reads.StaffAsync(manager, filter with { Page = int.MaxValue }, CancellationToken.None)).History!.Items);
    }

    private static Func<ReservationHistoryItem, IComparable> Key(string sort) => sort switch
    {
        "reference" => item => item.Receipt.Id,
        "vehicle" => item => $"{item.ManufactureYear} {item.Make} {item.Model} {item.Trim}",
        "buyer" => item => item.Receipt.Buyer.LegalName,
        "branch" => item => item.BranchName,
        "created" => item => item.Receipt.CreatedAt,
        "expires" => item => item.Receipt.ExpiresAt,
        "total" => item => item.Receipt.Price.Total,
        _ => item => item.Release?.Kind ?? "Active"
    };
}
