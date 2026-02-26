using Dealership.Application.Common;
using System.IdentityModel.Tokens.Jwt;
using Dealership.Application.Inspections;

namespace Dealership.Api.Endpoints;

public static class InspectionRepairEndpoints
{
    public static void MapInspectionRepairEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/staff/inspection").RequireAuthorization();
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        routes.MapPost("/drafts/{id:guid}/repair-requests", RequestAsync);
        routes.MapGet("/reports/{id:guid}/revisions/{revision:long}/repairs", ReadAsync);
        routes.MapPost("/drafts/{id:guid}/repair-approvals", ApproveAsync);
        routes.MapPost("/drafts/{id:guid}/repair-completions", CompleteAsync);
        routes.MapGet("/reports/{id:guid}/revisions/{revision:long}/repair-approvals", ReadApprovalAsync);
        routes.MapGet("/reports/{id:guid}/revisions/{revision:long}/repair-history", HistoryAsync);
    }

    private static async Task<IResult> CompleteAsync(Guid id, HttpContext context, InspectionRepairCompletionService completions,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var (request, error) = await JsonRequestReader.ReadAsync<InspectionRepairCompletionRequest>(context.Request, deadline.Token, 512 * 1024);
            if (error is not null)
            {
                return error;
            }

            var result = await completions.CompleteAsync(ActorId(context), id, request!, deadline.Token);
            return result.Status switch
            {
                InspectionAssignmentStatus.Succeeded => Results.Ok(result.Receipt),
                InspectionAssignmentStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide a repair request, valid versions and completion/reinspection notes."),
                InspectionAssignmentStatus.Forbidden => Results.Forbid(),
                InspectionAssignmentStatus.NotFound => Results.NotFound(),
                InspectionAssignmentStatus.Conflict => Results.Problem(statusCode: 409, title: "The inspection, repair request, approval or completion has changed."),
                _ => Results.Problem(statusCode: 500)
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Repair completion is unconfirmed; reconcile with the same operation and contents.");
        }
    }

    private static async Task<IResult> HistoryAsync(Guid id, long revision, int? page, int? pageSize, HttpContext context,
        IInspectionHistoryStore history, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await history.RepairsAsync(ActorId(context), id, revision, new(page ?? 1, pageSize ?? Pagination.DefaultPageSize), deadline.Token);
            return result.Status switch
            {
                InspectionAssignmentStatus.Succeeded => Results.Ok(result.History),
                InspectionAssignmentStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide a valid revision, page and page size."),
                InspectionAssignmentStatus.Forbidden => Results.Forbid(),
                InspectionAssignmentStatus.NotFound => Results.NotFound(),
                _ => Results.Problem(statusCode: 500)
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Repair history is temporarily unavailable.");
        }
    }

    private static async Task<IResult> ApproveAsync(Guid id, HttpContext context, InspectionRepairApprovalService approvals,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var (request, error) = await JsonRequestReader.ReadAsync<InspectionRepairApprovalRequest>(context.Request, deadline.Token, 512 * 1024);
            if (error is not null)
            {
                return error;
            }

            var result = await approvals.ApproveAsync(ActorId(context), id, request!, deadline.Token);
            return MapApproval(result);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Repair approval is unconfirmed; reconcile with the same operation and contents.");
        }
    }

    private static async Task<IResult> ReadApprovalAsync(Guid id, long revision, HttpContext context,
        IInspectionRepairApprovalStore approvals, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            return MapApproval(await approvals.ReadAsync(ActorId(context), id, revision, deadline.Token));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Repair approval is temporarily unavailable.");
        }
    }

    private static async Task<IResult> RequestAsync(Guid id, HttpContext context, InspectionRepairRequestService repairs,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var (request, error) = await JsonRequestReader.ReadAsync<InspectionRepairRequest>(context.Request, deadline.Token, 512 * 1024);
            if (error is not null)
            {
                return error;
            }

            var result = await repairs.RequestAsync(ActorId(context), id, request!, deadline.Token);
            return Map(result);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Repair request is unconfirmed; reconcile with the same operation and contents.");
        }
    }

    private static async Task<IResult> ReadAsync(Guid id, long revision, HttpContext context,
        IInspectionRepairRequestStore repairs, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            return Map(await repairs.ReadAsync(ActorId(context), id, revision, deadline.Token));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Repair request is temporarily unavailable.");
        }
    }

    private static Guid ActorId(HttpContext context) => Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    private static IResult MapApproval(InspectionRepairApprovalResult result) => result.Status switch
    {
        InspectionAssignmentStatus.Succeeded => Results.Ok(result.Receipt),
        InspectionAssignmentStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide a repair request and valid approval versions."),
        InspectionAssignmentStatus.Forbidden => Results.Forbid(),
        InspectionAssignmentStatus.NotFound => Results.NotFound(),
        InspectionAssignmentStatus.Conflict => Results.Problem(statusCode: 409, title: "The inspection, repair request or approval has changed."),
        _ => Results.Problem(statusCode: 500)
    };

    private static IResult Map(InspectionRepairResult result) => result.Status switch
    {
        InspectionAssignmentStatus.Succeeded => Results.Ok(result.Receipt),
        InspectionAssignmentStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide valid repair scope, budget and versions."),
        InspectionAssignmentStatus.Forbidden => Results.Forbid(),
        InspectionAssignmentStatus.NotFound => Results.NotFound(),
        InspectionAssignmentStatus.Conflict => Results.Problem(statusCode: 409, title: "The inspection, repair scope or operation has changed."),
        _ => Results.Problem(statusCode: 500)
    };
}
