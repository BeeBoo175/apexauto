import { formatDays } from "@/lib/performance/format";
import type { SalesRow, TeamTab, WorkshopRow } from "@/lib/performance/team";

export interface TeamColumn<Row> {
  key: string;
  label: string;
  // Absent when the measure does not apply to this row, such as a Now count for a transferred person.
  value: (row: Row) => number | null;
  text: (row: Row) => string;
  // Counts share a bar scale per column; elapsed days are shown as text only.
  bar: boolean;
  now?: boolean;
  // Measures left out of the table stay in the employee drawer and the export.
  detailOnly?: boolean;
}

function heldCount<Row>(
  key: string,
  label: string,
  value: (row: Row) => number | null,
  detailOnly = false,
): TeamColumn<Row> {
  return {
    key,
    label,
    value,
    text: (row) => {
      const held = value(row);
      return held === null ? "Not applicable" : String(held);
    },
    bar: true,
    now: true,
    detailOnly,
  };
}

function elapsed<Row>(
  key: string,
  label: string,
  value: (row: Row) => number | null,
  detailOnly = false,
): TeamColumn<Row> {
  return { key, label, value, text: (row) => formatDays(value(row)), bar: false, detailOnly };
}

function counted<Row>(
  key: string,
  label: string,
  value: (row: Row) => number,
  detailOnly = false,
): TeamColumn<Row> {
  return { key, label, value, text: (row) => String(value(row)), bar: true, detailOnly };
}

export const salesColumns: TeamColumn<SalesRow>[] = [
  counted("checkIns", "Check-ins", (row) => row.checkIns),
  counted("drivesCompleted", "Drives done", (row) => row.drivesCompleted),
  counted("drivesNotCompleted", "Drives not done", (row) => row.drivesNotCompleted, true),
  counted("salesSettled", "Settled", (row) => row.salesSettled),
  counted("salesDelivered", "Delivered", (row) => row.salesDelivered),
  counted("noShowCorrections", "No-show fixes", (row) => row.noShowCorrections, true),
  elapsed("assignmentToDelivery", "Time to deliver", (row) => row.assignmentToDeliveryDays),
  heldCount("intakeHandled", "Intake", (row) => row.intakeHandled, true),
  heldCount("testDrivesHeld", "Test drives", (row) => row.testDrivesHeld, true),
  heldCount("salesHandled", "Open sales", (row) => row.salesHandled),
];

export const workshopColumns: TeamColumn<WorkshopRow>[] = [
  counted("inspectionsSigned", "Inspections", (row) => row.inspectionsSigned),
  counted("reinspections", "Re-inspections", (row) => row.reinspections),
  counted("repairsCompleted", "Repairs", (row) => row.repairsCompleted),
  elapsed("assignmentToSignOff", "Time to sign off", (row) => row.assignmentToSignOffDays),
  elapsed(
    "assignmentToReinspection",
    "Re-inspection time",
    (row) => row.assignmentToReinspectionDays,
    true,
  ),
  heldCount("openDrafts", "Drafts", (row) => row.openDrafts),
  heldCount("openRepairs", "Repair work", (row) => row.openRepairs),
];

export const teamTitles: Record<TeamTab, string> = {
  sales: "Sales & service",
  workshop: "Workshop",
};
