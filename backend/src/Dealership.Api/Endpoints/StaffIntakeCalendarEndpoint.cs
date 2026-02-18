using System.IdentityModel.Tokens.Jwt;
using Dealership.Application.Intake;

namespace Dealership.Api.Endpoints;

public static class StaffIntakeCalendarEndpoint
{
    public static async Task<IResult> ReadAsync(HttpContext context, StaffCalendarService calendar,
        CancellationToken cancellationToken, string? from = null, string? to = null, string? status = null, Guid? branchId = null)
    {
        var query = StaffCalendarService.Parse(from, to, status, branchId);
        if (query is null)
        {
            return Results.Problem(statusCode: 400, title: "Choose a valid calendar period of at most 42 days.");
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var staffId = Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
            var result = await calendar.ReadAsync(staffId, query, deadline.Token);
            return result.Status switch
            {
                StaffCalendarStatus.Succeeded => Results.Ok(result.Snapshot),
                StaffCalendarStatus.Forbidden => Results.Forbid(),
                _ => Results.Problem(statusCode: 422, title: "More than 1,000 appointments match. Filter by branch or status.")
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "The calendar could not be refreshed.");
        }
    }
}
