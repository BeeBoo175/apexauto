"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import type { SignedReport } from "@/components/inspection/SignedReport";
import { useToast } from "@/components/ui/Toast";
import { inspectionFetch } from "@/lib/inspection/browser";
import { parseReport, parseRevisions, parseRepairs } from "@/lib/inspection/parse-report";
import type { Report, RevisionHistory, RepairHistory } from "@/lib/inspection/report-contracts";
import { DEFAULT_PAGE_SIZE, paginationQuery } from "@/lib/pagination";

interface ReportEntry {
  View: typeof SignedReport;
  report: Report;
  revisions: RevisionHistory | null;
  repairs: RepairHistory | null;
}

export function useSignedReportClientEntry(id: string, revision: number) {
  const [entry, setEntry] = useState<ReportEntry | null>(null);
  const [failed, setFailed] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const { error } = useToast();

  const load = useCallback(async () => {
    if (controller.current) {
      return;
    }
    const attempt = new AbortController();
    const signal = AbortSignal.any([attempt.signal, AbortSignal.timeout(22_000)]);
    controller.current = attempt;
    setFailed(false);
    const base = `reports/${id}/revisions`;
    try {
      const [report, revisions, repairs, { SignedReport: View }] = await Promise.all([
        inspectionFetch(`${base}/${revision}`, parseReport, { signal }),
        inspectionFetch(`${base}?${paginationQuery()}`, parseRevisions, { signal }).catch(
          () => null,
        ),
        inspectionFetch(`${base}/${revision}/repair-history?${paginationQuery()}`, parseRepairs, {
          signal,
        }).catch(() => null),
        import("@/components/inspection/SignedReport"),
      ]);
      if (attempt.signal.aborted || controller.current !== attempt) {
        return;
      }
      signal.throwIfAborted();
      if (
        report.status !== 200 ||
        report.data?.inspectionId !== id ||
        report.data.revision !== revision
      ) {
        throw new Error("Signed report unavailable");
      }
      const revisionHistory = revisions?.data ?? null;
      const repairHistory = repairs?.data ?? null;
      setEntry({
        View,
        report: report.data,
        revisions: matchesHistory(revisionHistory, id) ? revisionHistory : null,
        repairs:
          matchesHistory(repairHistory, id) && repairHistory?.revision === revision
            ? repairHistory
            : null,
      });
    } catch {
      if (!attempt.signal.aborted && controller.current === attempt) {
        setFailed(true);
        error("The signed report could not be loaded. Retry to open it.");
      }
    } finally {
      attempt.abort();
      if (controller.current === attempt) {
        controller.current = null;
      }
    }
  }, [id, revision, error]);

  useEffect(() => {
    let active = true;
    queueMicrotask(() => {
      if (active) {
        void load();
      }
    });
    return () => {
      active = false;
      controller.current?.abort();
      controller.current = null;
    };
  }, [load]);

  return { entry, failed, retry: load };
}

function matchesHistory(history: RevisionHistory | RepairHistory | null, id: string) {
  return (
    history?.inspectionId === id && history.page === 1 && history.pageSize === DEFAULT_PAGE_SIZE
  );
}
