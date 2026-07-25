using System.Net;
using System.Net.Http.Json;
using Dealership.Api.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;

namespace Dealership.IntegrationTests;

public class BoundedReadTests
{
    [Fact]
    public async Task DeadlineCancelsReadAndReturnsUnavailableWithoutCaching()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        var routes = app.MapGroup("/reads").NoStore();
        routes.MapGet("/delayed", (CancellationToken token) => BoundedRead.RunAsync(async deadline =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, deadline);
            return Results.NoContent();
        }, "Account history is temporarily unavailable.", token));
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/reads/delayed");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Account history is temporarily unavailable.", problem?.Title);
    }

    [Fact]
    public async Task CallerCancellationReachesReadAndIsNotMappedToUnavailable()
    {
        using var caller = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = BoundedRead.RunAsync(async deadline =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, deadline);
            return Results.NoContent();
        }, "Staff accounts are temporarily unavailable.", caller.Token);

        await started.Task;
        caller.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task CompletedResultAndUnexpectedFailureRetainTheirMeaning()
    {
        var expected = Results.NotFound();
        var actual = await BoundedRead.RunAsync(_ => Task.FromResult(expected),
            "Performance figures are temporarily unavailable.", CancellationToken.None);

        Assert.Same(expected, actual);
        await Assert.ThrowsAsync<InvalidOperationException>(() => BoundedRead.RunAsync(
            _ => throw new InvalidOperationException("Read failed."),
            "Performance figures are temporarily unavailable.", CancellationToken.None));
    }

    [Theory]
    [InlineData(200)]
    [InlineData(400)]
    [InlineData(404)]
    public async Task NoStorePreservesHandlerStatusOnGroupsAndIndividualRoutes(int status)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        app.MapGroup("/group").NoStore().MapGet("/read", () => Results.StatusCode(status));
        app.MapGet("/single", () => Results.StatusCode(status)).NoStore();
        await app.StartAsync();
        using var client = app.GetTestClient();

        foreach (var path in new[] { "/group/read", "/single" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(status, (int)response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
        }
    }
}
