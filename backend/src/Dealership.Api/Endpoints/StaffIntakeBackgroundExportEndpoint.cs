using Dealership.Application.Common;
using System.IdentityModel.Tokens.Jwt;
using Dealership.Application.Accounts;
using Dealership.Application.Intake;

namespace Dealership.Api.Endpoints;

public static class StaffIntakeBackgroundExportEndpoint
{
    public static async Task<IResult> ExportAsync(HttpContext context, IStaffExportWorker worker,
        IAccountSessionStore sessions, CancellationToken cancellationToken,
        string? from = null, string? to = null, string? status = null, Guid? branchId = null,
        string? search = null, string? sort = null, string? direction = null)
    {
        var query = StaffWorklistQuery.Parse(from, to, status, branchId, 1, Pagination.DefaultPageSize, search, sort, direction);
        if (query is null)
        {
            return Results.Problem(statusCode: 400, title: "Provide valid export filters and sorting.");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(StaffExportLimits.BackgroundDeadline);
        Stream? file = null;
        try
        {
            var staffId = Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
            var sessionId = Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Jti)!.Value);
            var actor = await sessions.GetActorAsync(staffId, sessionId, deadline.Token);
            if (actor is null)
            {
                return Results.Unauthorized();
            }
            if (actor.Role is not (AccountRoles.SalesAgent or AccountRoles.BranchManager or AccountRoles.GeneralManager))
            {
                return Results.Forbid();
            }
            if (context.Request.Headers["X-Expected-Actor"] != $"{actor.UserId}:{actor.Role}:{actor.BranchId}")
            {
                return Results.Problem(statusCode: 409, title: "Your account assignment changed. Retry after reloading.");
            }

            file = await worker.GenerateAsync(actor, query, deadline.Token);
            if (file is null)
            {
                return Results.Forbid();
            }
            if (await sessions.GetActorAsync(staffId, sessionId, deadline.Token) != actor)
            {
                return Results.Problem(statusCode: 409, title: "Your account assignment changed. Retry after reloading.");
            }

            var result = Results.Stream(file, "text/csv; charset=utf-8", "intake-appointments.csv");
            file = null;
            return result;
        }
        catch (StaffExportBusyException)
        {
            context.Response.Headers.RetryAfter = "1";
            return Results.Problem(statusCode: 429, title: "Another large export is running. Try again shortly.");
        }
        catch (StaffExportTooLargeException)
        {
            return Results.Problem(statusCode: 413, title: "The export exceeds the download size limit. Narrow the filters.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Export did not complete. Retry deliberately.");
        }
        finally
        {
            if (file is not null)
            {
                await file.DisposeAsync();
            }
        }
    }
}
