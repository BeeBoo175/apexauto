import type { CsvColumn } from "@/lib/csv";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { workshopStatusLabel, type WorkshopRow } from "./workshop-contracts";

export const workshopExportColumns: readonly CsvColumn<WorkshopRow>[] = [
  { label: "Registration", value: (row) => row.registrationPlate },
  { label: "Vehicle", value: (row) => `${row.make} ${row.model}` },
  { label: "Current work", value: (row) => workshopStatusLabel(row.status) },
  { label: "Assigned technician", value: (row) => row.technicianIdentity ?? "Unassigned" },
  { label: "Branch", value: (row) => row.branchName },
  { label: "Requested budget", value: (row) => row.budget?.toFixed(2) ?? "" },
  { label: "Since", value: (row) => formatMalaysianDateTime(row.since) },
  {
    label: "Date source",
    value: (row) => (row.dateSource === "Requested" ? "Repair requested" : "Vehicle checked in"),
  },
];
