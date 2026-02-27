import type { CsvColumn } from "@/lib/csv";
import type { WorklistItem } from "./contracts";

export const intakeExportColumns: readonly CsvColumn<WorklistItem>[] = [
  { label: "Registration", value: (row) => row.registrationPlate },
  { label: "Customer", value: (row) => row.customerName },
  { label: "Branch", value: (row) => row.branchName },
  { label: "Inspection start (UTC)", value: (row) => new Date(row.startsAt).toISOString() },
  { label: "Status", value: (row) => row.status },
  { label: "Appointment reference", value: (row) => row.id },
];
