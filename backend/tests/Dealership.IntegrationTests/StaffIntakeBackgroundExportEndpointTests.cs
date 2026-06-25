using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Dealership.Api.Endpoints;
using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using Microsoft.AspNetCore.Http;

namespace Dealership.IntegrationTests;

public sealed class StaffIntakeBackgroundExportEndpointTests
{
    [Fact]
    public async Task BusyAndOversizedExportsReturnRetryableErrorsWithoutAFile()
    {
        var actor = new CurrentActor(Guid.NewGuid(), AccountRoles.SalesAgent, Guid.NewGuid());
        var sessions = new Sessions(actor);
        var worker = new Worker { Error = new StaffExportBusyException() };
        var context = Context(actor);
        var busy = await ExportAsync(context, worker, sessions);
        Assert.Equal(429, Assert.IsType<IStatusCodeHttpResult>(busy, exactMatch: false).StatusCode);
        Assert.Equal("1", context.Response.Headers.RetryAfter);
        Assert.Null(context.Response.Headers.ContentDisposition.FirstOrDefault());
        worker.Error = new StaffExportTooLargeException();
        Assert.Equal(413, Assert.IsType<IStatusCodeHttpResult>(await ExportAsync(context, worker, sessions), exactMatch: false).StatusCode);
        worker.Error = null;
        var completed = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.FileStreamHttpResult>(
            await ExportAsync(context, worker, sessions));
        await completed.FileStream.DisposeAsync();
    }

    [Fact]
    public async Task InvalidQueriesAndForbiddenActorsNeverReachTheWorkerAndReassignmentDiscardsTheFile()
    {
        var actor = new CurrentActor(Guid.NewGuid(), AccountRoles.SalesAgent, Guid.NewGuid());
        var sessions = new Sessions(actor);
        var worker = new Worker();
        var invalid = await StaffIntakeBackgroundExportEndpoint.ExportAsync(Context(actor), worker, sessions,
            CancellationToken.None, sort: "untrusted");
        Assert.Equal(400, Assert.IsType<IStatusCodeHttpResult>(invalid, exactMatch: false).StatusCode);
        Assert.Equal(0, worker.Calls);
        sessions.Actor = actor with { Role = AccountRoles.Administrator, BranchId = null };
        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult>(await ExportAsync(Context(actor), worker, sessions));
        Assert.Equal(0, worker.Calls);
        sessions = new Sessions(actor) { ReassignOnSecondRead = true };
        var denied = await ExportAsync(Context(actor), worker, sessions);
        Assert.Equal(409, Assert.IsType<IStatusCodeHttpResult>(denied, exactMatch: false).StatusCode);
        Assert.False(worker.File!.CanRead);
    }

    private static Task<IResult> ExportAsync(HttpContext context, Worker worker, Sessions sessions)
    {
        return StaffIntakeBackgroundExportEndpoint.ExportAsync(context, worker, sessions,
            CancellationToken.None);
    }

    private static DefaultHttpContext Context(CurrentActor actor) => new()
    {
        Request = { Headers = { ["X-Expected-Actor"] = $"{actor.UserId}:{actor.Role}:{actor.BranchId}" } },
        User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(JwtRegisteredClaimNames.Sub, actor.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())]))
    };

    private sealed class Worker : IStaffExportWorker
    {
        public Exception? Error { get; set; }
        public int Calls { get; private set; }
        public Stream? File { get; private set; }

        public Task<Stream?> GenerateAsync(CurrentActor actor, StaffWorklistQuery query, CancellationToken cancellationToken)
        {
            Calls++;
            if (Error is not null)
            {
                return Task.FromException<Stream?>(Error);
            }
            File = new MemoryStream([1, 2, 3]);
            return Task.FromResult<Stream?>(File);
        }
    }

    private sealed class Sessions(CurrentActor actor) : IAccountSessionStore
    {
        private int _reads;
        public CurrentActor Actor { get; set; } = actor;
        public bool ReassignOnSecondRead { get; set; }

        public Task<CurrentActor?> GetActorAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
        {
            _reads++;
            var current = ReassignOnSecondRead && _reads > 1 ? Actor with { BranchId = Guid.NewGuid() } : Actor;
            return Task.FromResult<CurrentActor?>(current);
        }

        public Task<LoginSession?> LoginAsync(Guid loginKey, string email, string password, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<bool> IsUnverifiedStaffAsync(Guid userId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<bool> IsActiveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task RevokeAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
