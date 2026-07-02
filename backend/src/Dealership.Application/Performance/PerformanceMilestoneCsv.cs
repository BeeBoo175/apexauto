using System.Globalization;
using Dealership.Application.Common;

namespace Dealership.Application.Performance;

public static class PerformanceMilestoneCsv
{
    public static readonly string Header = "﻿" + Line("Milestone", "Reference", "Date (UTC)", "Branch",
        "Agreed sale total (MYR)");

    public static string Row(PerformanceMilestone milestone)
    {
        return Line(milestone.Kind, milestone.Reference,
            milestone.At.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
            milestone.BranchName,
            milestone.AgreedTotal?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty);
    }

    private static string Line(params string[] values) => string.Join(',', values.Select(CsvCell.Quote)) + "\r\n";
}
