"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { inspectionFetch } from "@/lib/inspection/browser";
import type { ApiResult } from "@/lib/api";
import { readRepairReview, type RepairReview } from "@/lib/inspection/repair-review";
import type { RepairSelection } from "./use-inspection-drawer";
import { useInitialToast } from "./use-initial-toast";

export function useRepairReview(
  inspectionId: string,
  selection: RepairSelection,
  initial: ApiResult<RepairReview> | null = null,
) {
  const [result, setResult] = useState(initial?.data ?? null);
  const [busy, setBusy] = useState(!initial);
  const [attempt, setAttempt] = useState(0);
  const controller = useRef<AbortController | null>(null);
  const { error } = useToast();
  const { requestId, revision } = selection;
  useInitialToast(initial?.message);

  useEffect(() => {
    if (initial && attempt === 0) {
      return;
    }
    const cancellation = new AbortController();
    controller.current = cancellation;
    const signal = AbortSignal.any([cancellation.signal, AbortSignal.timeout(22_000)]);

    async function load() {
      try {
        const next = await readRepairReview(
          inspectionId,
          { requestId, revision },
          inspectionFetch,
          signal,
        );
        setResult(next);
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
  }, [inspectionId, requestId, revision, attempt, error, initial]);

  function retry() {
    controller.current?.abort();
    setBusy(true);
    setAttempt((value) => value + 1);
  }

  return { result, busy, retry };
}
