import type { CsvColumn } from "@/lib/csv";
import type { WorklistItem } from "./contracts";

export const intakeExportColumns: readonly CsvColumn<WorklistItem>[] = [
  { label: "Registration", value: (row) => row.registrationPlate },
  {
    label: "Vehicle",
    value: (row) => `${row.manufactureYear} ${row.make} ${row.model} ${row.trim}`,
  },
  { label: "Status", value: (row) => row.status },
  { label: "Customer", value: (row) => row.customerName },
  { label: "Handler", value: (row) => row.handlerEmail ?? "Unassigned" },
  {
    label: "Handler eligible",
    value: (row) => {
      if (!row.handlerId) {
        return "";
      }
      return row.handlerEligible ? "Yes" : "No";
    },
  },
  { label: "Inspection start (UTC)", value: (row) => new Date(row.startsAt).toISOString() },
  { label: "Branch", value: (row) => row.branchName },
  { label: "Appointment reference", value: (row) => row.id },
];
