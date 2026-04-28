import { privateApiFetch } from "@/lib/api-browser";
import { downloadCsv, selectedCsv, type CsvColumn } from "@/lib/csv";
import { formatMalaysianDateTime } from "@/lib/date-format";
import {
  dealQuery,
  dealStatusLabels,
  parseDealPage,
  type DealFilters,
  type DealSummary,
} from "./deals";

export const dealExportColumns: readonly CsvColumn<DealSummary>[] = [
  { label: "Deal number", value: (row) => row.orderNumber ?? "" },
  { label: "Registration", value: (row) => row.registration },
  {
    label: "Vehicle",
    value: (row) => `${row.manufactureYear} ${row.make} ${row.model} ${row.trim}`.trim(),
  },
  { label: "Customer", value: (row) => row.customer },
  { label: "Status", value: (row) => dealStatusLabels[row.status] },
  { label: "Next step", value: (row) => row.nextStep },
  { label: "Balance payable (MYR)", value: (row) => row.balancePayable.toFixed(2) },
  { label: "Negotiated discount (MYR)", value: (row) => row.negotiatedDiscount.toFixed(2) },
  { label: "Agent", value: (row) => row.agent },
  { label: "Branch", value: (row) => row.branch },
  {
    label: "Submitted (Malaysia)",
    value: (row) => (row.submittedAtUtc ? formatMalaysianDateTime(row.submittedAtUtc) : ""),
  },
  { label: "Updated (Malaysia)", value: (row) => formatMalaysianDateTime(row.updatedAtUtc) },
  { label: "Reference", value: (row) => row.id },
];

export async function exportDeals(filters: DealFilters, cancellation: AbortSignal) {
  const signal = AbortSignal.any([cancellation, AbortSignal.timeout(120_000)]);
  const rows: DealSummary[] = [];
  const ids = new Set<string>();
  let total: number | null = null;
  let page = 1;
  while (total === null || rows.length < total) {
    const result = await privateApiFetch(
      `/api/staff/deals?${dealQuery({ ...filters, pageSize: 50 }, page)}`,
      parseDealPage,
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
        "Deal export could not be confirmed or exceeds 10,000 rows. Narrow the filters and retry.",
      );
    }
    if (
      (total !== null && total !== snapshot.total) ||
      snapshot.items.length !== Math.min(50, snapshot.total - rows.length)
    ) {
      throw new Error("Deals changed during export. Retry to download the current results.");
    }
    for (const row of snapshot.items) {
      if (ids.has(row.id)) {
        throw new Error("Deals changed during export. Retry to download the current results.");
      }
      ids.add(row.id);
      rows.push(row);
    }
    total = snapshot.total;
    page += 1;
  }
  signal.throwIfAborted();
  downloadCsv(
    new Blob([selectedCsv(rows, dealExportColumns)], { type: "text/csv;charset=utf-8" }),
    "deals-full.csv",
  );
}
