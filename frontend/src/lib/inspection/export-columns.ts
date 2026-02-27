import type { CsvColumn } from "@/lib/csv";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { label, type InspectionRow, type RepairRow } from "./contracts";

export const inspectionExportColumns: readonly CsvColumn<InspectionRow>[] = [
  { label: "Registration", value: (row) => row.registrationPlate },
  { label: "Vehicle", value: (row) => `${row.make} ${row.model}` },
  { label: "Branch", value: (row) => row.branchName },
  { label: "Checked in", value: (row) => formatMalaysianDateTime(row.checkedInAt) },
  { label: "Inspection", value: (row) => label(row.status) },
  { label: "Vehicle status", value: (row) => label(row.vehicleStatus) },
];

export const repairExportColumns: readonly CsvColumn<RepairRow>[] = [
  { label: "Registration", value: (row) => row.registrationPlate },
  { label: "Branch", value: (row) => row.branchName },
  { label: "Requested", value: (row) => formatMalaysianDateTime(row.requestedAt) },
  { label: "Revision / work", value: (row) => `${row.revision} / ${row.repairVersion}` },
  { label: "Budget", value: (row) => row.budget.toFixed(2) },
  { label: "Repair status", value: (row) => label(row.status) },
];
