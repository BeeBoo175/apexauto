"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { inspectionFetch } from "@/lib/inspection/browser";
import { parseReport, parseRepairs } from "@/lib/inspection/parse-report";
import type { RepairHistoryEntry, Report } from "@/lib/inspection/report-contracts";
import type { RepairSelection } from "./use-inspection-drawer";

export function useRepairReview(inspectionId: string, selection: RepairSelection) {
  const [result, setResult] = useState<{ report: Report; entry: RepairHistoryEntry } | null>(null);
  const [busy, setBusy] = useState(true);
  const [attempt, setAttempt] = useState(0);
  const controller = useRef<AbortController | null>(null);
  const { error } = useToast();
  const { requestId, revision } = selection;

  useEffect(() => {
    const cancellation = new AbortController();
    controller.current = cancellation;
    const signal = AbortSignal.any([cancellation.signal, AbortSignal.timeout(22_000)]);
    const base = `reports/${inspectionId}/revisions/${revision}`;

    async function load() {
      try {
        const reportRead = await inspectionFetch(base, parseReport, { signal });
        const report = reportRead.data;
        if (
          reportRead.status !== 200 ||
          report?.inspectionId !== inspectionId ||
          report.revision !== revision
        ) {
          throw new Error("Selected report unavailable");
        }
        let page = 1;
        while (true) {
          const history = await inspectionFetch(
            `${base}/repair-history?page=${page}&pageSize=50`,
            parseRepairs,
            { signal },
          );
          if (
            history.status !== 200 ||
            !history.data ||
            history.data.inspectionId !== inspectionId ||
            history.data.revision !== revision ||
            history.data.page !== page
          ) {
            throw new Error("Selected repair history unavailable");
          }
          const entry = history.data.items.find((item) => item.request.operationId === requestId);
          if (entry) {
            signal.throwIfAborted();
            setResult({ report, entry });
            return;
          }
          if (
            page * history.data.pageSize >= history.data.totalCount ||
            !history.data.items.length
          ) {
            throw new Error("Selected request unavailable");
          }
          page += 1;
        }
      } catch {
        if (!cancellation.signal.aborted) {
          error(
            "The selected request could not be refreshed. Existing work and signed findings remain shown.",
          );
        }
      } finally {
        if (!cancellation.signal.aborted) {
          setBusy(false);
        }
      }
    }
    void load();
    return () => cancellation.abort();
  }, [inspectionId, requestId, revision, attempt, error]);

  function retry() {
    controller.current?.abort();
    setBusy(true);
    setAttempt((value) => value + 1);
  }

  return { result, busy, retry };
}
