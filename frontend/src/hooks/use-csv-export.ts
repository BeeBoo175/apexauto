"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { downloadCsv, selectedCsv, type CsvColumn } from "@/lib/csv";

export function useCsvExport<Row>(
  columns: readonly CsvColumn<Row>[],
  fileName: string,
  onExportAll: (signal: AbortSignal) => Promise<void>,
) {
  const [busy, setBusy] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const toast = useToast();

  useEffect(() => {
    return () => {
      controller.current?.abort();
      controller.current = null;
    };
  }, []);

  async function exportAll() {
    if (controller.current) {
      return;
    }

    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);

    try {
      await onExportAll(attempt.signal);
    } catch (error) {
      if (!attempt.signal.aborted) {
        toast.error(error instanceof Error ? error.message : "CSV export could not be completed.");
      }
    } finally {
      if (controller.current === attempt) {
        controller.current = null;
        setBusy(false);
      }
    }
  }

  function exportSelected(rows: readonly Row[]) {
    if (controller.current || rows.length === 0) {
      return;
    }

    downloadCsv(
      new Blob([selectedCsv(rows, columns)], { type: "text/csv;charset=utf-8" }),
      `${fileName}-selected.csv`,
    );
  }

  return { busy, exportAll, exportSelected };
}
