using System.Globalization;
using Dealership.Application.Common;

namespace Dealership.Application.Performance;

// Team output per person with no money columns. Elapsed days are blank when the interval is not measured, and a
// Now count is blank for a row whose branch is no longer the person's current branch.
public static class PerformanceTeamCsv
{
    public static readonly string SalesHeader = Header("Name", "Role", "Branch", "Status", "Check-ins",
        "No-show corrections", "Drives completed", "Drives not completed", "Sales settled", "Sales delivered",
        "Assignment to delivery (elapsed days)", "Intake handled now", "Test drives held now", "Sales handled now");

    public static readonly string WorkshopHeader = Header("Name", "Role", "Branch", "Status", "Inspections signed",
        "Re-inspections", "Repairs completed", "Assignment to sign-off (elapsed days)",
        "Assignment to re-inspection sign-off (elapsed days)", "Open drafts now", "Open repairs now");

    public static string Row(PerformanceSalesRow row)
    {
        return Line(row.Name, row.Role, row.BranchName, row.Status, Number(row.CheckIns), Number(row.NoShowCorrections),
            Number(row.DrivesCompleted), Number(row.DrivesNotCompleted), Number(row.SalesSettled),
            Number(row.SalesDelivered), Days(row.AssignmentToDeliveryDays), Number(row.IntakeHandled),
            Number(row.TestDrivesHeld), Number(row.SalesHandled));
    }

    public static string Row(PerformanceWorkshopRow row)
    {
        return Line(row.Name, row.Role, row.BranchName, row.Status, Number(row.InspectionsSigned),
            Number(row.Reinspections), Number(row.RepairsCompleted), Days(row.AssignmentToSignOffDays),
            Days(row.AssignmentToReinspectionDays), Number(row.OpenDrafts), Number(row.OpenRepairs));
    }

    private static string Header(params string[] names) => "﻿" + Line(names);

    private static string Line(params string[] values) => string.Join(',', values.Select(CsvCell.Quote)) + "\r\n";

    private static string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Days(double? value) => value?.ToString("0.0", CultureInfo.InvariantCulture) ?? string.Empty;
}
