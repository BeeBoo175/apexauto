export interface CsvColumn<Row> {
  label: string;
  value: (row: Row) => string;
}

export function csvCell(value: string): string {
  const prefix = value.match(/^[\s\u0000-\u001f\u007f-\u009f]*/u)?.[0] ?? "";
  const first = value.slice(prefix.length, prefix.length + 1);
  const dangerous = /^[=+\-@]$/.test(first) || /[\u0000-\u001f\u007f-\u009f]/u.test(prefix);
  const safe = dangerous ? `'${value}` : value;
  return `"${safe.replaceAll('"', '""')}"`;
}

export function selectedCsv<Row>(rows: readonly Row[], columns: readonly CsvColumn<Row>[]) {
  const header = columns.map((column) => csvCell(column.label)).join(",");
  const body = rows.map((row) => columns.map((column) => csvCell(column.value(row))).join(","));
  return `\uFEFF${[header, ...body].join("\r\n")}\r\n`;
}

export function downloadCsv(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = filename;
  document.body.append(anchor);
  anchor.click();
  anchor.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}

export interface CsvPage<Row> {
  page: number;
  pageSize: number;
  total: number;
  items: Row[];
}

export interface PagedCsvExport<Row> {
  read: (
    page: number,
    pageSize: number,
    signal: AbortSignal,
  ) => Promise<{ status: number; data: CsvPage<Row> | null }>;
  id: (row: Row) => string;
  columns: readonly CsvColumn<Row>[];
  filename: string;
  // Shown when a page cannot be confirmed or the result is too large to export.
  unavailable: string;
  // Shown when the total or the rows move between pages.
  changed: string;
}

const exportPageSize = 50;
const exportRowLimit = 10_000;

// Reads every page of a filtered table and downloads it only if the pages agree with one another.
export async function exportPagedCsv<Row>(spec: PagedCsvExport<Row>, cancellation: AbortSignal) {
  const signal = AbortSignal.any([cancellation, AbortSignal.timeout(120_000)]);
  const rows: Row[] = [];
  const ids = new Set<string>();
  let total: number | null = null;
  let page = 1;
  while (total === null || rows.length < total) {
    const result = await spec.read(page, exportPageSize, signal);
    const snapshot = result.data;
    if (
      result.status !== 200 ||
      !snapshot ||
      snapshot.page !== page ||
      snapshot.pageSize !== exportPageSize ||
      snapshot.total > exportRowLimit
    ) {
      throw new Error(spec.unavailable);
    }
    if (
      (total !== null && total !== snapshot.total) ||
      snapshot.items.length !== Math.min(exportPageSize, snapshot.total - rows.length) ||
      snapshot.items.some((row) => ids.has(spec.id(row)))
    ) {
      throw new Error(spec.changed);
    }
    for (const row of snapshot.items) {
      ids.add(spec.id(row));
      rows.push(row);
    }
    total = snapshot.total;
    page += 1;
  }
  signal.throwIfAborted();
  downloadCsv(
    new Blob([selectedCsv(rows, spec.columns)], { type: "text/csv;charset=utf-8" }),
    spec.filename,
  );
}
