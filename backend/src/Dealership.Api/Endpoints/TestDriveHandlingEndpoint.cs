using Dealership.Application.TestDrives;

namespace Dealership.Api.Endpoints;

public static class TestDriveHandlingEndpoint
{
    public static async Task<IResult> StartAsync(Guid id, HttpContext context, TestDriveHandlingService handling,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var (input, error) = await JsonRequestReader.ReadAsync<StartInput>(context.Request, deadline.Token);
            if (error is not null)
            {
                return error;
            }

            if (input!.OperationId is null || input.AssignmentId is null || input.CustomerPresent is null ||
                input.DrivingLicenceChecked is null)
            {
                return Results.Problem(statusCode: 400, title: "Provide operation, assignment, attendance and licence affirmation.");
            }

            return Map(await handling.StartAsync(TestDriveCommandEndpoints.ActorId(context), id,
                new(input.OperationId.Value, input.AssignmentId.Value, input.CustomerPresent.Value,
                    input.DrivingLicenceChecked.Value), deadline.Token));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return TestDriveCommandEndpoints.Unconfirmed();
        }
    }

    public static async Task<IResult> OutcomeAsync(Guid id, HttpContext context, TestDriveHandlingService handling,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var (input, error) = await JsonRequestReader.ReadAsync<OutcomeInput>(context.Request, deadline.Token);
            if (error is not null)
            {
                return error;
            }

            if (input!.OperationId is null || input.AssignmentId is null || input.Kind is null || input.CustomerPresent is null)
            {
                return Results.Problem(statusCode: 400, title: "Provide operation, assignment, attendance and outcome.");
            }

            return Map(await handling.RecordOutcomeAsync(TestDriveCommandEndpoints.ActorId(context), id,
                new(input.OperationId.Value, input.AssignmentId.Value, input.Kind, input.CustomerPresent.Value,
                    input.Reason, input.Notes), deadline.Token));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return TestDriveCommandEndpoints.Unconfirmed();
        }
    }

    private static IResult Map(TestDriveHandlingResult result) => result.Status switch
    {
        TestDriveHandlingStatus.Succeeded => Results.Ok(new { result.Start, result.Outcome }),
        TestDriveHandlingStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide valid attendance and handling details."),
        TestDriveHandlingStatus.Forbidden => Results.Forbid(),
        TestDriveHandlingStatus.NotFound => Results.NotFound(),
        TestDriveHandlingStatus.Conflict => TestDriveCommandEndpoints.Conflict("test-drive-handling-conflict",
            "Ownership or operation contents changed."),
        TestDriveHandlingStatus.Unavailable => TestDriveCommandEndpoints.Conflict("test-drive-handling-unavailable",
            "The drive or vehicle is unavailable for this action."),
        TestDriveHandlingStatus.ScheduleConflict => TestDriveCommandEndpoints.Conflict("test-drive-schedule-conflict",
            "The vehicle or handler has conflicting work; retain the booked time and wait or cancel."),
        _ => Results.Problem(statusCode: 500)
    };

    private sealed record StartInput(Guid? OperationId, Guid? AssignmentId, bool? CustomerPresent, bool? DrivingLicenceChecked);

    private sealed record OutcomeInput(Guid? OperationId, Guid? AssignmentId, string? Kind,
        bool? CustomerPresent, string? Reason, string? Notes);
}
