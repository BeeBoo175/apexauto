"use client";

import type { SignedReport } from "@/components/inspection/SignedReport";
import { useStaffClientEntry } from "./use-staff-client-entry";
import { inspectionFetch } from "@/lib/inspection/browser";
import { parseReport } from "@/lib/inspection/parse-report";
import type { Report } from "@/lib/inspection/report-contracts";

interface ReportEntry {
  View: typeof SignedReport;
  report: Report;
}

export function useSignedReportClientEntry(id: string, revision: number) {
  return useStaffClientEntry<ReportEntry>(async (signal) => {
    const [report, { SignedReport: View }] = await Promise.all([
      inspectionFetch(`reports/${id}/revisions/${revision}`, parseReport, { signal }),
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
    return { View, report: report.data };
  }, "The signed report could not be loaded. Retry to open it.");
}
