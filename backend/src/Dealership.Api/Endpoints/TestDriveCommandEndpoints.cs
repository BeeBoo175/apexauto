using Dealership.Api.Configuration;
using Dealership.Application.Accounts;
using Dealership.Application.TestDrives;
using static Dealership.Api.Endpoints.HttpContextActor;

namespace Dealership.Api.Endpoints;

public static class TestDriveCommandEndpoints
{
    public static void MapTestDriveCommandEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api").RequireAuthorization().NoStore();
        routes.MapPost("/vehicles/{vehicleId:guid}/test-drives", BookAsync);
        routes.MapPost("/test-drives/{id:guid}/cancellations", CancelAsync);
        routes.MapPost("/staff/test-drives/{id:guid}/cancellations", CancelByStaffAsync);
        routes.MapPost("/staff/test-drives/{id:guid}/claims", TestDriveAssignmentEndpoints.ClaimAsync);
        routes.MapPost("/staff/test-drives/{id:guid}/assignments", TestDriveAssignmentEndpoints.AssignAsync);
        routes.MapPost("/staff/test-drives/{id:guid}/releases", TestDriveAssignmentEndpoints.ReleaseAsync);
        routes.MapPost("/staff/test-drives/{id:guid}/starts", TestDriveHandlingEndpoints.StartAsync);
        routes.MapPost("/staff/test-drives/{id:guid}/outcomes", TestDriveHandlingEndpoints.OutcomeAsync);
    }

    private static async Task<IResult> BookAsync(Guid vehicleId, HttpContext context,
        TestDriveBookingService bookings, IStaffAssistanceQuery assistance, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var (input, error) = await JsonRequestReader.ReadAsync<BookingInput>(context.Request, deadline.Token);
            if (error is not null)
            {
                return error;
            }

            if (input!.OperationId is null || input.PublicationId is null || input.StartsAt is null)
            {
                return Results.Problem(statusCode: 400, title: "Provide an operation ID, publication and booked start.");
            }

            var owner = await AssistedOwner.ResolveAsync(context, assistance, deadline.Token);
            if (owner.Error is not null)
            {
                return owner.Error;
            }

            var result = await bookings.BookAsync(owner.OwnerId, vehicleId,
                new(input.OperationId.Value, input.PublicationId.Value, input.StartsAt.Value), deadline.Token, owner.Staff);
            return result.Status switch
            {
                TestDriveBookingStatus.Succeeded => Results.Ok(result.Booking),
                TestDriveBookingStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide valid test-drive booking details."),
                TestDriveBookingStatus.Forbidden => Results.Forbid(),
                TestDriveBookingStatus.Conflict => Conflict("test-drive-operation-conflict", "The operation was used with different contents."),
                TestDriveBookingStatus.VehicleUnavailable => Conflict("test-drive-vehicle-unavailable", "This vehicle is unavailable for booking."),
                TestDriveBookingStatus.CustomerHasBooking => Conflict("customer-has-test-drive", "You already have an outstanding test drive."),
                TestDriveBookingStatus.SlotUnavailable => Conflict("test-drive-slot-unavailable", "This slot is unavailable; choose another time."),
                _ => Results.Problem(statusCode: 500)
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unconfirmed();
        }
    }

    private static Task<IResult> CancelAsync(Guid id, HttpContext context,
        TestDriveCancellationService cancellations, CancellationToken cancellationToken)
    {
        return CancelCoreAsync(id, context, cancellations, false, cancellationToken);
    }

    private static Task<IResult> CancelByStaffAsync(Guid id, HttpContext context,
        TestDriveCancellationService cancellations, CancellationToken cancellationToken)
    {
        return CancelCoreAsync(id, context, cancellations, true, cancellationToken);
    }

    private static async Task<IResult> CancelCoreAsync(Guid id, HttpContext context,
        TestDriveCancellationService cancellations, bool byStaff, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var (input, error) = await JsonRequestReader.ReadAsync<CancellationInput>(context.Request, deadline.Token);
            if (error is not null)
            {
                return error;
            }

            if (input!.OperationId is null || byStaff && input.Reason is null || !byStaff && input.Reason is not null)
            {
                return Results.Problem(statusCode: 400, title: "Provide an operation ID and a reason for staff cancellation.");
            }

            var result = byStaff
                ? await cancellations.CancelByStaffAsync(ActorId(context), id, input.OperationId.Value, input.Reason!, deadline.Token)
                : await cancellations.CancelAsync(ActorId(context), id, input.OperationId.Value, deadline.Token);
            return result.Status switch
            {
                TestDriveCancellationStatus.Succeeded => Results.Ok(result.Cancellation),
                TestDriveCancellationStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide valid cancellation details."),
                TestDriveCancellationStatus.Forbidden => Results.Forbid(),
                TestDriveCancellationStatus.NotFound => Results.NotFound(),
                TestDriveCancellationStatus.Conflict => Conflict("test-drive-cancellation-conflict", "The drive or operation has changed."),
                TestDriveCancellationStatus.TooLate => Conflict("test-drive-cancellation-too-late", "The cancellation deadline has passed."),
                _ => Results.Problem(statusCode: 500)
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unconfirmed();
        }
    }

    internal static IResult Unconfirmed() => Results.Problem(statusCode: 503,
        title: "Test-drive operation is unconfirmed; reconcile with the same operation ID and contents.");

    internal static IResult Conflict(string code, string title) => Results.Problem(statusCode: 409, title: title,
        extensions: new Dictionary<string, object?> { ["code"] = code });

    private sealed record BookingInput(Guid? OperationId, Guid? PublicationId, DateTimeOffset? StartsAt);

    private sealed record CancellationInput(Guid? OperationId, string? Reason);
}
