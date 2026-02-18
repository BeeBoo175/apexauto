using System.IdentityModel.Tokens.Jwt;
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
        staff.MapGet("/calendar", StaffIntakeCalendarEndpoint.ReadAsync);
        var routes = staff.MapGroup("/appointments");
        routes.MapGet("", ListAsync);
        routes.MapGet("/export", StaffIntakeExportEndpoint.ExportAsync);
        routes.MapPost("/export/background", StaffIntakeBackgroundExportEndpoint.ExportAsync);
        routes.MapGet("/{id:guid}", DetailAsync);
        routes.MapPost("/{id:guid}/check-in", CheckInAsync);
        routes.MapPost("/{id:guid}/no-show-correction", CorrectNoShowAsync);
    }

    private static async Task<IResult> ListAsync(HttpContext context, IStaffWorklistStore worklist,
        CancellationToken cancellationToken, string? from = null, string? to = null,
        string? status = null, Guid? branchId = null, int page = 1, int pageSize = 20,
        string? search = null, string? sort = null, string? direction = null)
    {
        var query = StaffWorklistQuery.Parse(from, to, status, branchId, page, pageSize, search, sort, direction);

        if (query is null)
        {
            return Results.Problem(statusCode: 400, title: "Provide valid dates, status, branch, search, sort and paging.");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));

        try
        {
            var result = await worklist.ListAsync(StaffId(context), query, deadline.Token);
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
            var result = await appointments.GetAsync(StaffId(context), id, deadline.Token);
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

    private static async Task<IResult> CheckInAsync(Guid id, HttpContext context, IAppointmentCheckInStore checkIns,
        CancellationToken cancellationToken)
    {
        return await SubmitCheckInAsync(id, context, checkIns, false, cancellationToken);
    }

    private static async Task<IResult> CorrectNoShowAsync(Guid id, HttpContext context, IAppointmentCheckInStore checkIns,
        CancellationToken cancellationToken)
    {
        return await SubmitCheckInAsync(id, context, checkIns, true, cancellationToken);
    }

    private static async Task<IResult> SubmitCheckInAsync(Guid id, HttpContext context, IAppointmentCheckInStore checkIns,
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

                return Map(await checkIns.CorrectNoShowAsync(StaffId(context), id, evidence,
                    new NoShowCorrectionRequest(value.AttendedAt.Value, value.Reason), deadline.Token));
            }

            return Map(await checkIns.CheckInAsync(StaffId(context), id, evidence, deadline.Token));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503,
                title: "Check-in is unconfirmed; reconcile using the same appointment ID and contents.");
        }
    }

    private static Guid StaffId(HttpContext context) => Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    private static IResult Map(CheckInResult result)
    {
        return result.Status switch
        {
            CheckInStatus.Succeeded => Results.Ok(result.Receipt),
            CheckInStatus.Forbidden => Results.Forbid(),
            CheckInStatus.NotFound => Results.NotFound(),
            CheckInStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide valid VIN, odometer and physical confirmations."),
            CheckInStatus.Conflict => Results.Problem(statusCode: 409, title: "The appointment was checked in with different contents."),
            CheckInStatus.Unavailable => Results.Problem(statusCode: 409, title: "The appointment or its evidence is unavailable for check-in."),
            CheckInStatus.VehicleConflict => Results.Problem(statusCode: 409, title: "The VIN or registration already identifies a vehicle."),
            _ => Results.Problem(statusCode: 500)
        };
    }

    private sealed record CheckInRequest(string? Vin, int? MileageKilometres, bool CustomerPresent,
        bool IdentityConfirmed, bool OwnershipConfirmed, bool VehicleConfirmed, string? Notes,
        DateTimeOffset? AttendedAt = null, string? Reason = null);
}
