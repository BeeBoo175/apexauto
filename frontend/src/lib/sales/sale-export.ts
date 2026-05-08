import { privateApiFetch } from "@/lib/api-browser";
import { exportPagedCsv, type CsvColumn } from "@/lib/csv";
import { formatMalaysianDateTime } from "@/lib/date-format";
import {
  saleStatusLabels,
  saleReference,
  type SaleSummary,
  type SalesTableFilters,
} from "./contracts";
import { parseSalesTable } from "./parse";
import { salesTablePath, tableQuery } from "./query";

const when = (value: string | null) => (value ? formatMalaysianDateTime(value) : "");

// The table's shared columns plus every view-specific value, so one file serves any tab.
export const saleExportColumns: readonly CsvColumn<SaleSummary>[] = [
  { label: "Reference", value: (row) => saleReference(row) },
  { label: "Reservation", value: (row) => row.reservationNumber },
  { label: "Registration", value: (row) => row.registration },
  {
    label: "Vehicle",
    value: (row) => `${row.manufactureYear} ${row.make} ${row.model} ${row.trim}`.trim(),
  },
  { label: "Customer", value: (row) => row.customer },
  { label: "Agent", value: (row) => row.agent ?? "" },
  { label: "Branch", value: (row) => row.branch },
  { label: "Status", value: (row) => saleStatusLabels[row.status] },
  { label: "Negotiated discount (MYR)", value: (row) => row.negotiatedDiscount.toFixed(2) },
  { label: "Balance payable (MYR)", value: (row) => row.balancePayable.toFixed(2) },
  { label: "Expires (Malaysia)", value: (row) => when(row.dueAt) },
  { label: "Claimed (Malaysia)", value: (row) => when(row.claimedAt) },
  { label: "Submitted (Malaysia)", value: (row) => when(row.submittedAt) },
  { label: "Approved (Malaysia)", value: (row) => when(row.decidedAt) },
  { label: "Closed (Malaysia)", value: (row) => when(row.closedAt) },
];

export function exportSales(filters: SalesTableFilters, cancellation: AbortSignal) {
  return exportPagedCsv(
    {
      read: (page, pageSize, signal) =>
        privateApiFetch(
          `${salesTablePath}?${tableQuery({ ...filters, pageSize }, page)}`,
          parseSalesTable,
          { signal },
        ),
      id: (row) => row.id,
      columns: saleExportColumns,
      filename: "sales-full.csv",
      unavailable:
        "Sales export could not be confirmed or exceeds 10,000 rows. Narrow the filters and retry.",
      changed: "Sales changed during export. Retry to download the current results.",
    },
    cancellation,
  );
}
