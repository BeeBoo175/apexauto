import { privateApiFetch } from "@/lib/api-browser";
import { downloadCsv, selectedCsv, type CsvColumn } from "@/lib/csv";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { saleStatusLabels } from "./sale";
import {
  parseSalesTable,
  saleReference,
  salesTablePath,
  tableQuery,
  type SaleSummary,
  type SalesTableFilters,
} from "./sales";

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

export async function exportSales(filters: SalesTableFilters, cancellation: AbortSignal) {
  const signal = AbortSignal.any([cancellation, AbortSignal.timeout(120_000)]);
  const rows: SaleSummary[] = [];
  const ids = new Set<string>();
  let total: number | null = null;
  let page = 1;
  while (total === null || rows.length < total) {
    const result = await privateApiFetch(
      `${salesTablePath}?${tableQuery({ ...filters, pageSize: 50 }, page)}`,
      parseSalesTable,
      { signal },
    );
    const snapshot = result.data;
    if (
      result.status !== 200 ||
      !snapshot ||
      snapshot.page !== page ||
      snapshot.pageSize !== 50 ||
      snapshot.total > 10_000
    ) {
      throw new Error(
        "Sales export could not be confirmed or exceeds 10,000 rows. Narrow the filters and retry.",
      );
    }
    if (
      (total !== null && total !== snapshot.total) ||
      snapshot.items.length !== Math.min(50, snapshot.total - rows.length) ||
      snapshot.items.some((row) => ids.has(row.id))
    ) {
      throw new Error("Sales changed during export. Retry to download the current results.");
    }
    for (const row of snapshot.items) {
      ids.add(row.id);
      rows.push(row);
    }
    total = snapshot.total;
    page += 1;
  }
  signal.throwIfAborted();
  downloadCsv(
    new Blob([selectedCsv(rows, saleExportColumns)], { type: "text/csv;charset=utf-8" }),
    "sales-full.csv",
  );
}
