"use client";

import { useCsvExport } from "./use-csv-export";
import type { CsvColumn, CsvPage } from "@/lib/csv";
import { exportPagedCsv } from "@/lib/csv";

// Exports are the shown rows read page by page with the same period, branch and attribution, then downloaded
// only if the pages agree with one another.
export function usePerformanceExport<Row>(
  fileName: string,
  columns: readonly CsvColumn<Row>[],
  read: (
    page: number,
    pageSize: number,
    signal: AbortSignal,
  ) => Promise<{ status: number; data: CsvPage<Row> | null }>,
  id: (row: Row) => string,
  label: string,
) {
  return useCsvExport(columns, fileName, (signal) =>
    exportPagedCsv(
      {
        read,
        id,
        columns,
        filename: `${fileName}.csv`,
        unavailable: `${label} could not be exported. Narrow the period or branch and retry.`,
        changed: `${label} changed while exporting. Retry the export.`,
      },
      signal,
    ),
  );
}
