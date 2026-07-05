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
}

function heldCount<Row>(
  key: string,
  label: string,
  value: (row: Row) => number | null,
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
  };
}

function elapsed<Row>(
  key: string,
  label: string,
  value: (row: Row) => number | null,
): TeamColumn<Row> {
  return { key, label, value, text: (row) => formatDays(value(row)), bar: false };
}

function counted<Row>(key: string, label: string, value: (row: Row) => number): TeamColumn<Row> {
  return { key, label, value, text: (row) => String(value(row)), bar: true };
}

export const salesColumns: TeamColumn<SalesRow>[] = [
  counted("checkIns", "Check-ins", (row) => row.checkIns),
  counted("drivesCompleted", "Drives completed", (row) => row.drivesCompleted),
  counted("drivesNotCompleted", "Drives not completed", (row) => row.drivesNotCompleted),
  counted("salesSettled", "Sales settled", (row) => row.salesSettled),
  counted("salesDelivered", "Sales delivered", (row) => row.salesDelivered),
  counted("noShowCorrections", "No-show corrections", (row) => row.noShowCorrections),
  elapsed("assignmentToDelivery", "Assignment to delivery", (row) => row.assignmentToDeliveryDays),
  heldCount("intakeHandled", "Intake handled", (row) => row.intakeHandled),
  heldCount("testDrivesHeld", "Test drives held", (row) => row.testDrivesHeld),
  heldCount("salesHandled", "Sales handled", (row) => row.salesHandled),
];

export const workshopColumns: TeamColumn<WorkshopRow>[] = [
  counted("inspectionsSigned", "Inspections signed", (row) => row.inspectionsSigned),
  counted("reinspections", "Re-inspections", (row) => row.reinspections),
  counted("repairsCompleted", "Repairs completed", (row) => row.repairsCompleted),
  elapsed("assignmentToSignOff", "Assignment to sign-off", (row) => row.assignmentToSignOffDays),
  elapsed(
    "assignmentToReinspection",
    "Assignment to re-inspection sign-off",
    (row) => row.assignmentToReinspectionDays,
  ),
  heldCount("openDrafts", "Open drafts", (row) => row.openDrafts),
  heldCount("openRepairs", "Open repair work", (row) => row.openRepairs),
];

export const teamTitles: Record<TeamTab, string> = {
  sales: "Sales & service",
  workshop: "Workshop",
};
