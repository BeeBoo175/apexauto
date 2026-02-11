using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Dealership.Application.Intake;

namespace Dealership.Api.Endpoints;

public static class StaffIntakeExportEndpoint
{
    public static async Task<IResult> ExportAsync(HttpContext context, IStaffWorklistStore worklist,
        TimeProvider clock, CancellationToken cancellationToken, string? from = null, string? to = null,
        string? status = null, Guid? branchId = null, string? search = null, string? sort = null, string? direction = null)
    {
        var query = StaffWorklistQuery.Parse(from, to, status, branchId, 1, 20, clock.GetUtcNow(), search, sort, direction);
        if (query is null)
        {
            return Results.Problem(statusCode: 400, title: "Provide valid export filters and sorting.");
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var staffId = Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
            var result = await worklist.ExportAsync(staffId, query, deadline.Token);
            if (result is null)
            {
                return Results.Forbid();
            }
            if (result.Total > 10000)
            {
                return Results.Problem(statusCode: 422, title: "More than 10,000 matching appointments. Narrow the filters before exporting.");
            }
            var headings = new[] { "Registration", "Customer", "Branch", "Inspection start (UTC)", "Status", "Appointment reference" };
            var csv = new StringBuilder("\uFEFF").AppendJoin(',', headings.Select(CsvCell)).Append("\r\n");
            foreach (var item in result.Items)
            {
                deadline.Token.ThrowIfCancellationRequested();
                var values = new[] { item.RegistrationPlate, item.CustomerName, item.BranchName,
                    item.StartsAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture),
                    item.Status, item.Id.ToString() };
                csv.AppendJoin(',', values.Select(CsvCell)).Append("\r\n");
            }
            context.Response.Headers["X-Export-Row-Count"] = result.Total.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Results.File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv; charset=utf-8", "intake-appointments.csv");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Export did not complete. Narrow the filters or retry deliberately.");
        }
    }

    internal static string CsvCell(string value)
    {
        var index = 0;
        var controlPrefix = false;
        while (index < value.Length && (char.IsWhiteSpace(value[index]) || char.IsControl(value[index]) || value[index] == '\uFEFF'))
        {
            controlPrefix |= char.IsControl(value[index]);
            index++;
        }
        if (controlPrefix || (index < value.Length && "=+-@".Contains(value[index])))
        {
            value = "'" + value;
        }
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
