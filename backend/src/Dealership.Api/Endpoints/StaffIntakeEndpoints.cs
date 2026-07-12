using Dealership.Application.Common;
using Dealership.Application.Intake;
using Dealership.Domain.Intake;

namespace Dealership.Api.Endpoints;

public static class StaffIntakeEndpoints
{
    public static void MapStaffIntakeEndpoints(this WebApplication app)
    {
        var staff = app.MapGroup("/api/staff/intake").RequireAuthorization();
        staff.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        var routes = staff.MapGroup("/appointments");
        routes.MapGet("", ListAsync);
        routes.MapGet("/export", StaffIntakeExportEndpoint.ExportAsync);
        routes.MapPost("/export/background", StaffIntakeBackgroundExportEndpoint.ExportAsync);
        routes.MapGet("/{id:guid}", DetailAsync);
        routes.MapGet("/{id:guid}/handlers", HandlersAsync);
        routes.MapPost("/{id:guid}/claims", StaffIntakeAssignmentEndpoints.ClaimAsync);
        routes.MapPost("/{id:guid}/assignments", StaffIntakeAssignmentEndpoints.AssignAsync);
        routes.MapPost("/{id:guid}/releases", StaffIntakeAssignmentEndpoints.ReleaseAsync);
        routes.MapPost("/{id:guid}/check-ins", CheckInAsync);
        routes.MapPost("/{id:guid}/no-show-corrections", CorrectNoShowAsync);
    }

    private static async Task<IResult> ListAsync(HttpContext context, IStaffWorklistStore worklist,
        CancellationToken cancellationToken, string? from = null, string? to = null,
        string? status = null, Guid? branchId = null, int page = 1, int pageSize = Pagination.DefaultPageSize,
        string? search = null, string? sort = null, string? direction = null,
        string? registration = null, string? vehicle = null, string? customer = null,
        string? queue = null, Guid? handlerId = null, string? handler = null)
    {
        var query = StaffWorklistQuery.Parse(from, to, status, branchId, page, pageSize, search, sort, direction, registration, vehicle, customer, queue, handlerId, handler);

        if (query is null)
        {
            return Results.Problem(statusCode: 400, title: "Provide valid dates, status, branch, search, sort and paging.");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));

        try
        {
            var result = await worklist.ListAsync(HttpContextActor.ActorId(context), query, deadline.Token);
            return result is null ? Results.Forbid() : Results.Ok(result);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "The staff worklist is temporarily unavailable.");
        }
    }

    private static async Task<IResult> DetailAsync(Guid id, HttpContext context, IStaffAppointmentDetailStore appointments,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));

        try
        {
            var result = await appointments.GetAsync(HttpContextActor.ActorId(context), id, deadline.Token);
            return result.Status switch
            {
                StaffAppointmentDetailStatus.Succeeded => Results.Ok(result.Detail),
                StaffAppointmentDetailStatus.Forbidden => Results.Forbid(),
                StaffAppointmentDetailStatus.NotFound => Results.NotFound(),
                _ => Results.Problem(statusCode: 500)
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "The staff appointment is temporarily unavailable.");
        }
    }

    private static async Task<IResult> HandlersAsync(Guid id, HttpContext context, IStaffAppointmentDetailStore appointments,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await appointments.HandlersAsync(HttpContextActor.ActorId(context), id, deadline.Token);
            return result.Status switch
            {
                StaffAppointmentDetailStatus.Succeeded => Results.Ok(result.Handlers),
                StaffAppointmentDetailStatus.Forbidden => Results.Forbid(),
                _ => Results.NotFound()
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Appointment handlers are temporarily unavailable.");
        }
    }

    private static async Task<IResult> CheckInAsync(Guid id, HttpContext context, AppointmentCheckInService checkIns,
        CancellationToken cancellationToken)
    {
        return await SubmitCheckInAsync(id, context, checkIns, false, cancellationToken);
    }

    private static async Task<IResult> CorrectNoShowAsync(Guid id, HttpContext context, AppointmentCheckInService checkIns,
        CancellationToken cancellationToken)
    {
        return await SubmitCheckInAsync(id, context, checkIns, true, cancellationToken);
    }

    private static async Task<IResult> SubmitCheckInAsync(Guid id, HttpContext context, AppointmentCheckInService checkIns,
        bool correction, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));

        try
        {
            var (request, error) = await JsonRequestReader.ReadAsync<CheckInRequest>(context.Request, deadline.Token);

            if (error is not null)
            {
                return error;
            }

            var value = request!;

            if (value.MileageKilometres is null)
            {
                return Results.Problem(statusCode: 400, title: "Provide the actual odometer reading.");
            }

            var evidence = new CheckInEvidence(value.Vin ?? string.Empty, value.MileageKilometres.Value,
                value.CustomerPresent, value.IdentityConfirmed, value.OwnershipConfirmed, value.VehicleConfirmed, value.Notes);
            if (correction)
            {
                if (value.AttendedAt is null || string.IsNullOrWhiteSpace(value.Reason))
                {
                    return Results.Problem(statusCode: 400, title: "Provide original-date attendance time and a correction reason.");
                }

                return Map(await checkIns.CorrectNoShowAsync(HttpContextActor.ActorId(context), id, evidence,
                    new NoShowCorrectionRequest(value.AttendedAt.Value, value.Reason), deadline.Token));
            }

            return Map(await checkIns.CheckInAsync(HttpContextActor.ActorId(context), id, evidence, deadline.Token));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503,
                title: "Check-in is unconfirmed; reconcile using the same appointment ID and contents.");
        }
    }

    private static IResult Map(CheckInResult result)
    {
        return result.Status switch
        {
            CheckInStatus.Succeeded => Results.Ok(result.Receipt),
            CheckInStatus.Forbidden => Results.Forbid(),
            CheckInStatus.NotFound => Results.NotFound(),
            CheckInStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide valid VIN, odometer and physical confirmations."),
            CheckInStatus.Conflict => Results.Problem(statusCode: 409,
                title: "The appointment was checked in with different contents.",
                extensions: new Dictionary<string, object?> { ["code"] = "check-in-content-conflict" }),
            CheckInStatus.Unavailable => Results.Problem(statusCode: 409,
                title: "The appointment or its evidence is unavailable for check-in.",
                extensions: new Dictionary<string, object?> { ["code"] = "check-in-unavailable" }),
            CheckInStatus.VehicleConflict => Results.Problem(statusCode: 409,
                title: "The VIN or registration already identifies a vehicle.",
                extensions: new Dictionary<string, object?> { ["code"] = "vehicle-identity-conflict" }),
            _ => Results.Problem(statusCode: 500)
        };
    }

    private sealed record CheckInRequest(string? Vin, int? MileageKilometres, bool CustomerPresent,
        bool IdentityConfirmed, bool OwnershipConfirmed, bool VehicleConfirmed, string? Notes,
        DateTimeOffset? AttendedAt = null, string? Reason = null);
}
