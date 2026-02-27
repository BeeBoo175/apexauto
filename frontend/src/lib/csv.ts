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
