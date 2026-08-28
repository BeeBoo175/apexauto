using Dealership.Api.Configuration;
using Dealership.Application.Common;
using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using static Dealership.Api.Endpoints.HttpContextActor;

namespace Dealership.Api.Endpoints;

public static class IntakeBookingEndpoints
{
    public static void MapIntakeBookingEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/intake/appointments").RequireAuthorization().NoStore();
        routes.MapPost("", BookAsync);
        routes.MapGet("", ListAsync);
        routes.MapGet("/{id:guid}", GetReceiptAsync);
        routes.MapPost("/{id:guid}/cancellations", CancelAsync);
    }

    private static async Task<IResult> CancelAsync(Guid id, HttpContext context, AppointmentCancellationService cancellations,
        CancellationToken cancellationToken)
    {
        return await BoundedCommand.RunAsync(async deadline =>
        {
            var result = await cancellations.CancelAsync(ActorId(context), id, deadline);
            return result.Status switch
            {
                CancellationStatus.Succeeded => Results.Ok(result.Receipt),
                CancellationStatus.Forbidden => Results.Forbid(),
                CancellationStatus.NotFound => Results.NotFound(),
                CancellationStatus.TooLate => Results.Problem(statusCode: 409, title: "The appointment has already started."),
                CancellationStatus.CheckedIn => Results.Problem(statusCode: 409, title: "The appointment is already checked in."),
                _ => Results.BadRequest()
            };
        },
            () => Results.Problem(statusCode: 503, title: "Cancellation is unconfirmed; reconcile using the same appointment ID."), cancellationToken);
    }

    private static async Task<IResult> ListAsync(HttpContext context, IAppointmentHistoryStore history,
        CancellationToken cancellationToken, string? from = null, string? to = null, int page = 1, int pageSize = Pagination.DefaultPageSize)
    {
        var query = AppointmentHistoryQuery.Parse(from, to, page, pageSize);

        if (query is null)
        {
            return Results.Problem(statusCode: 400, title: "Provide a valid paired date range of at most 366 days and valid paging.");
        }

        return await BoundedCommand.RunAsync(async deadline =>
        {
            var result = await history.ListAsync(ActorId(context), query, deadline);
            return result is null ? Results.Forbid() : Results.Ok(result);
        },
            () => Results.Problem(statusCode: 503, title: "Appointment history is temporarily unavailable."), cancellationToken);
    }

    private static async Task<IResult> BookAsync(HttpContext context, IntakeBookingService bookings,
        IStaffAssistanceQuery assistance, CancellationToken cancellationToken)
    {
        return await BoundedCommand.RunAsync(async deadline =>
        {
            var (request, error) = await JsonRequestReader.ReadAsync<BookingRequest>(context.Request, deadline);

            if (error is not null)
            {
                return error;
            }

            var owner = await AssistedOwner.ResolveAsync(context, assistance, deadline);
            if (owner.Error is not null)
            {
                return owner.Error;
            }

            return Map(await bookings.BookAsync(owner.OwnerId, request!, deadline, owner.Staff));
        },
            () => Results.Problem(statusCode: 503, title: "Booking is unconfirmed; reconcile using the same booking ID and contents."), cancellationToken);
    }

    private static async Task<IResult> GetReceiptAsync(Guid id, HttpContext context, IntakeBookingService bookings,
        IStaffAssistanceQuery assistance, CancellationToken cancellationToken)
    {
        var owner = await AssistedOwner.ResolveAsync(context, assistance, cancellationToken);
        if (owner.Error is not null)
        {
            return owner.Error;
        }

        return Map(await bookings.GetReceiptAsync(owner.OwnerId, id, cancellationToken, owner.Staff));
    }

    private static IResult Map(BookingResult result)
    {
        return result.Status switch
        {
            BookingStatus.Succeeded => Results.Ok(result.Receipt),
            BookingStatus.Forbidden => Results.Forbid(),
            BookingStatus.NotFound => Results.NotFound(),
            BookingStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide valid vehicle, registration, quote and four photo IDs."),
            BookingStatus.QuoteChanged => Results.Json(result.FreshQuote, statusCode: 409),
            BookingStatus.Conflict => Results.Problem(statusCode: 409, title: "Booking ID already belongs to a different request."),
            BookingStatus.SlotUnavailable => Results.Problem(statusCode: 409, title: "The branch or inspection slot is unavailable."),
            BookingStatus.PhotosUnavailable => Results.Problem(statusCode: 409, title: "Provide four available, unexpired and unused owned photo angles."),
            _ => Results.Problem(statusCode: 500)
        };
    }
}
