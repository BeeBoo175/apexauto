using System.IdentityModel.Tokens.Jwt;
using Dealership.Application.Intake;
using Dealership.Domain.Intake;

namespace Dealership.Api.Endpoints;

public static class StaffIntakeEndpoints
{
    public static void MapStaffIntakeEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/staff/intake/appointments").RequireAuthorization();
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        routes.MapGet("", ListAsync);
        routes.MapPost("/{id:guid}/check-in", CheckInAsync);
    }

    private static async Task<IResult> ListAsync(HttpContext context, IStaffWorklistStore worklist,
        TimeProvider clock, CancellationToken cancellationToken, string? from = null, string? to = null,
        string? status = null, Guid? branchId = null, int page = 1, int pageSize = 20)
    {
        var query = StaffWorklistQuery.Parse(from, to, status, branchId, page, pageSize, clock.GetUtcNow());

        if (query is null)
        {
            return Results.Problem(statusCode: 400, title: "Provide a valid date range, status, branch and paging.");
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

    private static async Task<IResult> CheckInAsync(Guid id, HttpContext context, IAppointmentCheckInStore checkIns,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));

        try
        {
            var request = await JsonRequestReader.ReadAsync<CheckInRequest>(context.Request, deadline.Token);

            if (request.Error is not null)
            {
                return request.Error;
            }

            var value = request.Value!;

            if (value.MileageKilometres is null)
            {
                return Results.Problem(statusCode: 400, title: "Provide the actual odometer reading.");
            }

            var evidence = new CheckInEvidence(value.Vin ?? string.Empty, value.MileageKilometres.Value,
                value.CustomerPresent, value.IdentityConfirmed, value.OwnershipConfirmed, value.VehicleConfirmed, value.Notes);
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
        bool IdentityConfirmed, bool OwnershipConfirmed, bool VehicleConfirmed, string? Notes);
}
