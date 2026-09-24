using System.Text;
using Dealership.Application.Common;
using Dealership.Application.Intake;

namespace Dealership.Api.Endpoints;

public static class StaffIntakeExportEndpoint
{
    public static async Task<IResult> ExportAsync(HttpContext context, IStaffWorklistStore worklist,
        CancellationToken cancellationToken, string? from = null, string? to = null,
        string? view = null, Guid? branchId = null, string? search = null, string? sort = null, string? direction = null,
        string? registration = null, string? vehicle = null, string? customer = null,
        Guid? handlerId = null, string? handler = null)
    {
        var query = StaffWorklistQuery.Parse(from, to, view, branchId, 1, Pagination.DefaultPageSize, search, sort, direction, registration, vehicle, customer, handlerId, handler);
        if (query is null)
        {
            return Results.Problem(statusCode: 400, title: "Provide valid export filters and sorting.");
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var staffId = HttpContextActor.ActorId(context);
            var result = await worklist.ExportAsync(staffId, query, deadline.Token);
            if (result is null)
            {
                return Results.Forbid();
            }
            if (result.Total > StaffExportLimits.ImmediateRows)
            {
                return Results.Problem(statusCode: 422, title: "This export requires the background worker.");
            }
            var csv = new StringBuilder(StaffIntakeCsv.Header);
            foreach (var item in result.Items)
            {
                deadline.Token.ThrowIfCancellationRequested();
                csv.Append(StaffIntakeCsv.Row(item));
            }
            context.Response.Headers["X-Export-Row-Count"] = result.Total.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Results.File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv; charset=utf-8", "intake-appointments.csv");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Export did not complete. Narrow the filters or retry deliberately.");
        }
    }
}
