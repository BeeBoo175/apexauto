import { downloadCsv, selectedCsv } from "@/components/ui/data-table/csv";
import type { DataColumn } from "@/components/ui/data-table/types";
import type { Page } from "@/lib/intake/contracts";
import type { QueueFilters, QueueKind } from "./contracts";
import { inspectionFetch } from "./browser";
import { queueQuery } from "./query";

export async function exportQueue<T>(
  kind: QueueKind,
  filters: QueueFilters,
  columns: DataColumn<T>[],
  parse: (value: unknown) => Page<T> | null,
  rowId: (row: T) => string,
  cancellation: AbortSignal,
) {
  const signal = AbortSignal.any([cancellation, AbortSignal.timeout(120_000)]);
  const rows: T[] = [];
  const ids = new Set<string>();
  let total: number | null = null;
  let page = 1;

  // Reuse scoped reads; never present a partial or shifting page set as a full export.
  while (total === null || rows.length < total) {
    const result = await inspectionFetch(
      `${kind}?${queueQuery({ ...filters, pageSize: 50 }, page)}`,
      parse,
      { signal },
    );
    const snapshot = result.data;
    if (result.status !== 200 || !snapshot || snapshot.page !== page || snapshot.pageSize !== 50) {
      throw new Error("CSV export could not be confirmed. Existing rows remain shown.");
    }
    if (snapshot.total > 10_000) {
      throw new Error("The export exceeds 10,000 rows. Narrow the filters.");
    }
    if (total !== null && total !== snapshot.total) {
      throw new Error("The results changed during export. Retry to download the current results.");
    }
    total = snapshot.total;
    const expected = Math.min(50, total - rows.length);
    if (snapshot.items.length !== expected || snapshot.items.some((row) => ids.has(rowId(row)))) {
      throw new Error("The results changed during export. Retry to download the current results.");
    }
    for (const row of snapshot.items) {
      if (ids.has(rowId(row))) {
        throw new Error("The export contains repeated records. Retry the export.");
      }
      ids.add(rowId(row));
      rows.push(row);
    }
    page += 1;
  }
  signal.throwIfAborted();
  downloadCsv(
    new Blob([selectedCsv(rows, columns)], { type: "text/csv;charset=utf-8" }),
    `${kind}-full.csv`,
  );
}
