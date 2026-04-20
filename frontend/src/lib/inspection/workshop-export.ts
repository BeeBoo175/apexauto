import type { CsvColumn } from "@/lib/csv";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { workshopStatusLabel, type WorkshopRow } from "./workshop-contracts";

export const workshopExportColumns: readonly CsvColumn<WorkshopRow>[] = [
  { label: "Registration", value: (row) => row.registrationPlate },
  {
    label: "Vehicle",
    value: (row) => `${row.manufactureYear} ${row.make} ${row.model} ${row.trim}`,
  },
  { label: "Current work", value: (row) => workshopStatusLabel(row.status) },
  { label: "Assigned technician", value: (row) => row.technicianIdentity ?? "Unassigned" },
  { label: "Branch", value: (row) => row.branchName },
  { label: "Requested budget", value: (row) => row.budget?.toFixed(2) ?? "" },
  { label: "Checked in", value: (row) => formatMalaysianDateTime(row.checkedInAt) },
  {
    label: "Repair requested",
    value: (row) => (row.requestedAt ? formatMalaysianDateTime(row.requestedAt) : ""),
  },
];
