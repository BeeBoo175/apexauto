using Dealership.Api.Configuration;
using Dealership.Application.TestDrives;

namespace Dealership.Api.Endpoints;

public static class TestDriveHandlingEndpoints
{
    public static async Task<IResult> StartAsync(Guid id, HttpContext context, TestDriveHandlingService handling,
        CancellationToken cancellationToken)
    {
        return await BoundedCommand.RunAsync(async deadline =>
        {
            var (input, error) = await JsonRequestReader.ReadAsync<StartInput>(context.Request, deadline);
            if (error is not null)
            {
                return error;
            }

            if (input!.OperationId is null || input.AssignmentId is null || input.CustomerPresent is null ||
                input.DrivingLicenceChecked is null)
            {
                return Results.Problem(statusCode: 400, title: "Provide operation, assignment, attendance and licence affirmation.");
            }

            return Map(await handling.StartAsync(HttpContextActor.ActorId(context), id,
                new(input.OperationId.Value, input.AssignmentId.Value, input.CustomerPresent.Value,
                    input.DrivingLicenceChecked.Value), deadline));
        }, TestDriveCommandEndpoints.Unconfirmed, cancellationToken);
    }

    public static async Task<IResult> OutcomeAsync(Guid id, HttpContext context, TestDriveHandlingService handling,
        CancellationToken cancellationToken)
    {
        return await BoundedCommand.RunAsync(async deadline =>
        {
            var (input, error) = await JsonRequestReader.ReadAsync<OutcomeInput>(context.Request, deadline);
            if (error is not null)
            {
                return error;
            }

            if (input!.OperationId is null || input.AssignmentId is null || input.Kind is null || input.CustomerPresent is null)
            {
                return Results.Problem(statusCode: 400, title: "Provide operation, assignment, attendance and outcome.");
            }

            return Map(await handling.RecordOutcomeAsync(HttpContextActor.ActorId(context), id,
                new(input.OperationId.Value, input.AssignmentId.Value, input.Kind, input.CustomerPresent.Value,
                    input.Reason, input.Notes), deadline));
        }, TestDriveCommandEndpoints.Unconfirmed, cancellationToken);
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
