using Dealership.Application.Common;
using System.IdentityModel.Tokens.Jwt;
using Dealership.Application.Inspections;

namespace Dealership.Api.Endpoints;

public static class InspectionDraftEndpoints
{
    public static void MapInspectionDraftEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/staff/inspection").RequireAuthorization();
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        routes.MapPost("/vehicles/{id:guid}/assignment", AssignAsync);
        routes.MapGet("/technicians", TechniciansAsync);
        routes.MapGet("/drafts/{id:guid}", ReadAsync);
        routes.MapPost("/drafts/{id:guid}/saves", SaveAsync);
        routes.MapPost("/drafts/{id:guid}/sign", SignAsync);
        routes.MapPost("/drafts/{id:guid}/reopen", ReopenAsync);
        routes.MapGet("/reports/{id:guid}/revisions/{revision:long}", ReportAsync);
        routes.MapGet("/reports/{id:guid}/revisions", HistoryAsync);
    }

    private static async Task<IResult> TechniciansAsync(int? page, int? pageSize, HttpContext context,
        IInspectionAssignmentStore assignments, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await assignments.TechniciansAsync(ActorId(context), page ?? 1,
                pageSize ?? Pagination.DefaultPageSize, deadline.Token);
            return Map(result.Status, result.Technicians);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Workshop technician choices are temporarily unavailable.");
        }
    }

    private static async Task<IResult> HistoryAsync(Guid id, int? page, int? pageSize, HttpContext context,
        IInspectionHistoryStore history, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await history.RevisionsAsync(ActorId(context), id, new(page ?? 1, pageSize ?? Pagination.DefaultPageSize), deadline.Token);
            return Map(result.Status, result.History);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Inspection history is temporarily unavailable.");
        }
    }

    private static async Task<IResult> ReopenAsync(Guid id, HttpContext context, InspectionReopeningService reports,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var (request, error) = await JsonRequestReader.ReadAsync<InspectionReopenRequest>(context.Request, deadline.Token);
            if (error is not null)
            {
                return error;
            }

            var result = await reports.ReopenAsync(ActorId(context), id, request!, deadline.Token);
            return Map(result.Status, result.Receipt);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Reopening is unconfirmed; reconcile with the same operation and contents.");
        }
    }

    private static async Task<IResult> SignAsync(Guid id, HttpContext context, InspectionSignoffService reports,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var (request, error) = await JsonRequestReader.ReadAsync<InspectionSignRequest>(context.Request, deadline.Token);
            if (error is not null)
            {
                return error;
            }

            var result = await reports.SignAsync(ActorId(context), id, request!, deadline.Token);
            return Map(result.Status, result.Report);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Sign-off is unconfirmed; reconcile with the same operation and contents.");
        }
    }

    private static async Task<IResult> ReportAsync(Guid id, long revision, HttpContext context,
        IInspectionReportStore reports, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await reports.ReadAsync(ActorId(context), id, revision, deadline.Token);
            return Map(result.Status, result.Report);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Inspection report is temporarily unavailable.");
        }
    }

    private static async Task<IResult> SaveAsync(Guid id, HttpContext context, InspectionDraftSaveService saves,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var (request, error) = await JsonRequestReader.ReadAsync<InspectionDraftSaveRequest>(context.Request, deadline.Token, 512 * 1024);
            if (error is not null)
            {
                return error;
            }

            var result = await saves.SaveAsync(ActorId(context), id, request!, deadline.Token);
            return Map(result.Status, result.Receipt);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Save is unconfirmed; reconcile with the same operation and contents.");
        }
    }

    private static async Task<IResult> AssignAsync(Guid id, HttpContext context, InspectionAssignmentService assignments,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var (request, error) = await JsonRequestReader.ReadAsync<InspectionAssignmentRequest>(context.Request, deadline.Token);
            if (error is not null)
            {
                return error;
            }

            var result = await assignments.AssignAsync(ActorId(context), id, request!, deadline.Token);
            return Map(result.Status, result.Receipt);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Assignment is unconfirmed; reconcile with the same operation and contents.");
        }
    }

    private static async Task<IResult> ReadAsync(Guid id, HttpContext context, IInspectionAssignmentStore assignments,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await assignments.ReadAsync(ActorId(context), id, deadline.Token);
            return Map(result.Status, result.Draft);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Inspection draft is temporarily unavailable.");
        }
    }

    private static Guid ActorId(HttpContext context) => Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    private static IResult Map(InspectionAssignmentStatus status, object? value)
    {
        return status switch
        {
            InspectionAssignmentStatus.Succeeded => Results.Ok(value),
            InspectionAssignmentStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide valid inspection identities, versions and exception details."),
            InspectionAssignmentStatus.Forbidden => Results.Forbid(),
            InspectionAssignmentStatus.NotFound => Results.NotFound(),
            InspectionAssignmentStatus.Conflict => Results.Problem(statusCode: 409, title: "The vehicle, assignment or operation has changed."),
            _ => Results.Problem(statusCode: 500)
        };
    }
}
