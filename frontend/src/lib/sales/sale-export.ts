import { privateApiFetch } from "@/lib/api-browser";
import { exportPagedCsv, type CsvColumn } from "@/lib/csv";
import {
  saleStatusLabels,
  saleReference,
  remainingHoldSeconds,
  saleDiscountOverLimit,
  saleHasHoldTimer,
  type SaleSummary,
  type SalesTableFilters,
} from "./contracts";
import { parseSalesTable } from "./parse";
import { salesTablePath, tableQuery } from "./query";

export function saleExportColumns(showBranch: boolean, now: number): CsvColumn<SaleSummary>[] {
  const columns: CsvColumn<SaleSummary>[] = [
    { label: "Reference", value: (row) => saleReference(row) },
    { label: "Registration", value: (row) => row.registration },
    {
      label: "Vehicle",
      value: (row) => `${row.manufactureYear} ${row.make} ${row.model} ${row.trim}`.trim(),
    },
    { label: "Customer", value: (row) => row.customer },
    { label: "Agent", value: (row) => row.agent ?? "" },
    { label: "Status", value: (row) => saleStatusLabels[row.status] },
    { label: "Negotiated discount (MYR)", value: (row) => row.negotiatedDiscount.toFixed(2) },
    { label: "Discount over limit", value: (row) => (saleDiscountOverLimit(row) ? "Yes" : "No") },
    { label: "Balance payable (MYR)", value: (row) => row.balancePayable.toFixed(2) },
    {
      label: "Time left (hours)",
      value: (row) =>
        saleHasHoldTimer(row.status) ? (remainingHoldSeconds(row, now) / 3600).toFixed(2) : "",
    },
    {
      label: "Hold timer",
      value: (row) => {
        if (!saleHasHoldTimer(row.status)) {
          return "";
        }
        return row.pausedSeconds === null ? "Running" : "Paused";
      },
    },
  ];
  if (showBranch) {
    columns.push({ label: "Branch", value: (row) => row.branch });
  }
  return columns;
}

export function exportSales(
  filters: SalesTableFilters,
  cancellation: AbortSignal,
  showBranch: boolean,
) {
  return exportPagedCsv(
    {
      read: (page, pageSize, signal) =>
        privateApiFetch(
          `${salesTablePath}?${tableQuery({ ...filters, pageSize }, page)}`,
          parseSalesTable,
          { signal },
        ),
      id: (row) => row.id,
      columns: saleExportColumns(showBranch, Date.now()),
      filename: "sales-full.csv",
      unavailable:
        "Sales export could not be confirmed or exceeds 10,000 rows. Narrow the filters and retry.",
      changed: "Sales changed during export. Retry to download the current results.",
    },
    cancellation,
  );
}
