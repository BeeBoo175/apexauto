using System.Globalization;

namespace Dealership.Application.Intake;

public static class StaffIntakeCsv
{
    public static readonly string Header = "\uFEFF" + string.Join(',',
        new[] { "Registration", "Vehicle", "Status", "Customer", "Handler", "Handler eligible", "Inspection start (UTC)", "Branch", "Appointment reference" }
            .Select(Cell)) + "\r\n";

    public static string Row(StaffWorklistItem item)
    {
        var vehicle = $"{item.ManufactureYear} {item.Make} {item.Model} {item.Trim}";
        var eligibility = item.HandlerEligible ? "Yes" : "No";
        if (item.HandlerId is null)
        {
            eligibility = "";
        }
        var values = new[] { item.RegistrationPlate, vehicle, item.Status, item.CustomerName,
            item.HandlerEmail ?? "Unassigned", eligibility,
            item.StartsAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
            item.BranchName, item.Id.ToString() };
        return string.Join(',', values.Select(Cell)) + "\r\n";
    }

    private static string Cell(string value)
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
