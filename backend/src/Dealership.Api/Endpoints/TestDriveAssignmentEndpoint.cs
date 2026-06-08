using Dealership.Domain.Common;
using Dealership.Application.TestDrives;
using Dealership.Domain.TestDrives;

namespace Dealership.Api.Endpoints;

public static class TestDriveAssignmentEndpoint
{
    public static Task<IResult> ClaimAsync(Guid id, HttpContext context, TestDriveAssignmentService assignments,
        CancellationToken cancellationToken)
    {
        return SubmitAsync(id, context, assignments, WorkOwnershipKind.Claimed, cancellationToken);
    }

    public static Task<IResult> AssignAsync(Guid id, HttpContext context, TestDriveAssignmentService assignments,
        CancellationToken cancellationToken)
    {
        return SubmitAsync(id, context, assignments, WorkOwnershipKind.Assigned, cancellationToken);
    }

    public static Task<IResult> ReleaseAsync(Guid id, HttpContext context, TestDriveAssignmentService assignments,
        CancellationToken cancellationToken)
    {
        return SubmitAsync(id, context, assignments, WorkOwnershipKind.Released, cancellationToken);
    }

    private static async Task<IResult> SubmitAsync(Guid id, HttpContext context, TestDriveAssignmentService assignments,
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

            var result = await assignments.ExecuteAsync(TestDriveCommandEndpoints.ActorId(context), id, kind,
                new(input.OperationId.Value, input.ExpectedVersion.Value, input.HandlerId, input.Reason), deadline.Token);
            return Map(result);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return TestDriveCommandEndpoints.Unconfirmed();
        }
    }

    private static IResult Map(TestDriveAssignmentResult result) => result.Status switch
    {
        TestDriveAssignmentStatus.Succeeded => Results.Ok(new { result.Receipt, result.Current }),
        TestDriveAssignmentStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide a valid ownership command."),
        TestDriveAssignmentStatus.Forbidden => Results.Forbid(),
        TestDriveAssignmentStatus.NotFound => Results.NotFound(),
        TestDriveAssignmentStatus.Conflict => Conflict(result, "test-drive-ownership-conflict", "Ownership or operation contents changed."),
        TestDriveAssignmentStatus.Unavailable => Conflict(result, "test-drive-ownership-unavailable", "The drive or handler is unavailable for assignment."),
        TestDriveAssignmentStatus.ScheduleConflict => Conflict(result, "test-drive-schedule-conflict", "The handler already has conflicting work."),
        _ => Results.Problem(statusCode: 500)
    };

    private static IResult Conflict(TestDriveAssignmentResult result, string code, string title) =>
        Results.Problem(statusCode: 409, title: title,
            extensions: new Dictionary<string, object?> { ["code"] = code, ["current"] = result.Current });

    private sealed record AssignmentInput(Guid? OperationId, long? ExpectedVersion, Guid? HandlerId, string? Reason);
}
