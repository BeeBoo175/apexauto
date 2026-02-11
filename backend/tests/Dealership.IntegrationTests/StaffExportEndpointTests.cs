using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Dealership.Api.Endpoints;
using Dealership.Application.Intake;
using Microsoft.AspNetCore.Http;

namespace Dealership.IntegrationTests;

public sealed class StaffExportEndpointTests
{
    [Fact]
    public async Task ExportValidatesQueryPropagatesScopeAndRefusesOversizeWithoutPartialFile()
    {
        var staffId = Guid.NewGuid();
        var context = Context(staffId);
        var store = new ExportStore(new StaffWorklistPage([], 10001, 1, 10000));
        var invalid = await StaffIntakeExportEndpoint.ExportAsync(context, store, TimeProvider.System,
            CancellationToken.None, sort: "untrusted");
        Assert.Equal(400, Assert.IsAssignableFrom<IStatusCodeHttpResult>(invalid).StatusCode);
        Assert.Null(store.Query);
        var refused = await StaffIntakeExportEndpoint.ExportAsync(context, store, TimeProvider.System,
            CancellationToken.None, search: " VCA ", sort: "customerName", direction: "desc");
        Assert.Equal(422, Assert.IsAssignableFrom<IStatusCodeHttpResult>(refused).StatusCode);
        Assert.False(refused is Microsoft.AspNetCore.Http.HttpResults.FileContentHttpResult);
        Assert.Equal(staffId, store.StaffId);
        Assert.Equal("VCA", store.Query!.Search);
        Assert.Equal("customerName", store.Query.Sort);
        Assert.Equal("desc", store.Query.Direction);
        Assert.Null(context.Response.Headers.ContentDisposition.FirstOrDefault());
        store.Result = null;
        var denied = await StaffIntakeExportEndpoint.ExportAsync(context, store, TimeProvider.System, CancellationToken.None);
        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult>(denied);
    }

    [Fact]
    public async Task ExportIncludesTheExactCapWithSafeCellsAndExplicitUtcHeadings()
    {
        var item = new StaffWorklistItem(Guid.NewGuid(), Guid.NewGuid(), " \u0001@branch", " \t=SUM(1,2)",
            "VCA \"9701\"\nA", new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.FromHours(8)),
            DateTimeOffset.UtcNow, "Booked");
        var store = new ExportStore(new StaffWorklistPage(Enumerable.Repeat(item, 10000).ToArray(), 10000, 1, 10000));
        var context = Context(Guid.NewGuid());
        var result = Assert.IsAssignableFrom<Microsoft.AspNetCore.Http.HttpResults.FileContentHttpResult>(await StaffIntakeExportEndpoint.ExportAsync(
            context, store, TimeProvider.System, CancellationToken.None));
        var csv = Encoding.UTF8.GetString(result.FileContents.Span);
        Assert.StartsWith("\uFEFF\"Registration\",\"Customer\",\"Branch\",\"Inspection start (UTC)\",\"Status\",\"Appointment reference\"\r\n", csv);
        Assert.Contains("\"' \t=SUM(1,2)\"", csv);
        Assert.Contains("\"' \u0001@branch\"", csv);
        Assert.Contains("\"VCA \"\"9701\"\"\nA\"", csv);
        Assert.Contains("\"2026-10-20T01:00:00.000Z\"", csv);
        Assert.Equal("10000", context.Response.Headers["X-Export-Row-Count"]);
    }

    [Fact]
    public async Task ExportDeadlineRefusesAStalledReadAndCallerCancellationPropagates()
    {
        var store = new ExportStore(null) { Stall = true };
        var expired = await StaffIntakeExportEndpoint.ExportAsync(Context(Guid.NewGuid()), store,
            TimeProvider.System, CancellationToken.None);
        Assert.Equal(503, Assert.IsAssignableFrom<IStatusCodeHttpResult>(expired).StatusCode);
        using var caller = new CancellationTokenSource();
        caller.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => StaffIntakeExportEndpoint.ExportAsync(
            Context(Guid.NewGuid()), store, TimeProvider.System, caller.Token));
    }

    private static DefaultHttpContext Context(Guid id) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, id.ToString())]))
    };

    private sealed class ExportStore(StaffWorklistPage? result) : IStaffWorklistStore
    {
        public StaffWorklistPage? Result { get; set; } = result;
        public StaffWorklistQuery? Query { get; private set; }
        public Guid StaffId { get; private set; }
        public bool Stall { get; init; }

        public Task<StaffWorklistPage?> ListAsync(Guid staffId, StaffWorklistQuery query, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Export must not page the list port.");

        public async Task<StaffWorklistPage?> ExportAsync(Guid staffId, StaffWorklistQuery query, CancellationToken cancellationToken)
        {
            StaffId = staffId;
            Query = query;
            if (Stall)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            return Result;
        }
    }
}
