"use client";

import type { SignedReport } from "@/components/inspection/SignedReport";
import { useStaffClientEntry } from "./use-staff-client-entry";
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
  return useStaffClientEntry<ReportEntry>(async (signal) => {
    const base = `reports/${id}/revisions`;
    const [report, revisions, repairs, { SignedReport: View }] = await Promise.all([
      inspectionFetch(`${base}/${revision}`, parseReport, { signal }),
      inspectionFetch(`${base}?${paginationQuery()}`, parseRevisions, { signal }).catch(() => null),
      inspectionFetch(`${base}/${revision}/repair-history?${paginationQuery()}`, parseRepairs, {
        signal,
      }).catch(() => null),
      import("@/components/inspection/SignedReport"),
    ]);
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
    return {
      View,
      report: report.data,
      revisions: matchesHistory(revisionHistory, id) ? revisionHistory : null,
      repairs:
        matchesHistory(repairHistory, id) && repairHistory?.revision === revision
          ? repairHistory
          : null,
    };
  }, "The signed report could not be loaded. Retry to open it.");
}

function matchesHistory(history: RevisionHistory | RepairHistory | null, id: string) {
  return (
    history?.inspectionId === id && history.page === 1 && history.pageSize === DEFAULT_PAGE_SIZE
  );
}
