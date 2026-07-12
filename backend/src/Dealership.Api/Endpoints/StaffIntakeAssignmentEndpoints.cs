using Dealership.Domain.Common;
using Dealership.Application.Intake;
using Dealership.Domain.Intake;

namespace Dealership.Api.Endpoints;

public static class StaffIntakeAssignmentEndpoints
{
    public static Task<IResult> ClaimAsync(Guid id, HttpContext context, AppointmentAssignmentService assignments,
        CancellationToken cancellationToken)
    {
        return SubmitAsync(id, context, assignments, WorkOwnershipKind.Claimed, cancellationToken);
    }

    public static Task<IResult> AssignAsync(Guid id, HttpContext context, AppointmentAssignmentService assignments,
        CancellationToken cancellationToken)
    {
        return SubmitAsync(id, context, assignments, WorkOwnershipKind.Assigned, cancellationToken);
    }

    public static Task<IResult> ReleaseAsync(Guid id, HttpContext context, AppointmentAssignmentService assignments,
        CancellationToken cancellationToken)
    {
        return SubmitAsync(id, context, assignments, WorkOwnershipKind.Released, cancellationToken);
    }

    private static async Task<IResult> SubmitAsync(Guid id, HttpContext context, AppointmentAssignmentService assignments,
        string kind, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var (input, error) = await JsonRequestReader.ReadAsync<AssignmentInput>(context.Request, deadline.Token);
            if (error is not null)
            {
                return error;
            }

            if (input!.OperationId is null || input.ExpectedVersion is null)
            {
                return Results.Problem(statusCode: 400, title: "Provide an operation ID and expected ownership version.");
            }

            var actorId = HttpContextActor.ActorId(context);
            var request = new AppointmentAssignmentRequest(input.OperationId.Value, input.ExpectedVersion.Value,
                input.HandlerId, input.Reason);
            return Map(await assignments.ExecuteAsync(actorId, id, kind, request, deadline.Token));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503,
                title: "Ownership is unconfirmed; reconcile with the same operation and contents.");
        }
    }

    private static IResult Map(AppointmentAssignmentResult result)
    {
        return result.Status switch
        {
            AppointmentAssignmentStatus.Succeeded => Results.Ok(new { result.Receipt, result.Current }),
            AppointmentAssignmentStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide a valid ownership command."),
            AppointmentAssignmentStatus.Forbidden => Results.Forbid(),
            AppointmentAssignmentStatus.NotFound => Results.NotFound(),
            AppointmentAssignmentStatus.Conflict => Conflict(result, "intake-ownership-conflict",
                "Ownership changed or the operation was used with different contents."),
            AppointmentAssignmentStatus.Unavailable => Conflict(result, "intake-ownership-unavailable",
                "The appointment or handler is unavailable for assignment."),
            AppointmentAssignmentStatus.ScheduleConflict => Conflict(result, "intake-schedule-conflict",
                "The handler already has work in this booked interval."),
            _ => Results.Problem(statusCode: 500)
        };
    }

    private static IResult Conflict(AppointmentAssignmentResult result, string code, string title)
    {
        return Results.Problem(statusCode: 409, title: title,
            extensions: new Dictionary<string, object?> { ["code"] = code, ["current"] = result.Current });
    }

    private sealed record AssignmentInput(Guid? OperationId, long? ExpectedVersion, Guid? HandlerId, string? Reason);
}
