"use client";
import { useEffect, useRef, useState } from "react";
import { inspectionFetch } from "@/lib/inspection/browser";
import { dateRangeError } from "@/lib/intake/list-query";
import type { ApiResult, Branch, Page } from "@/lib/intake/contracts";
import { parseBranch, parsePage } from "@/lib/intake/parse";
import { intakeFetch } from "@/lib/intake/browser";
import type { QueueFilters, QueueKind } from "@/lib/inspection/contracts";
import { queuePath, queueQuery } from "@/lib/inspection/query";
import { useToast } from "@/components/ui/Toast";
import { useInitialToast } from "./use-initial-toast";

export function useInspectionQueue<T>(
  kind: QueueKind,
  initial: ApiResult<Page<T>>,
  filters: QueueFilters,
  page: number,
  initialBranches: Page<Branch> | null,
  parse: (v: unknown) => Page<T> | null,
) {
  const [snapshot, setSnapshot] = useState(initial.data);
  const [active, setActive] = useState({ filters, page });
  const [draft, setDraft] = useState(filters);
  const [branches, setBranches] = useState(initialBranches);
  const [busy, setBusy] = useState(false);
  const [ready, setReady] = useState(false);
  const [entry, setEntry] = useState(initial);
  const [revision, setRevision] = useState(0);
  if (entry !== initial) {
    setEntry(initial);
    if (initial.data) {
      setSnapshot(initial.data);
      setActive({ filters, page });
      setDraft(filters);
      setRevision((value) => value + 1);
    }
  }
  const controller = useRef<AbortController | null>(null);
  const toast = useToast();
  useInitialToast(initial.message);
  useEffect(() => {
    queueMicrotask(() => setReady(true));
    return () => controller.current?.abort();
  }, []);

  async function load(nextFilters = active.filters, nextPage = active.page) {
    if (controller.current) {
      return;
    }
    const invalid = dateRangeError(nextFilters);
    if (invalid) {
      toast.error(invalid);
      return;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    try {
      const result = await inspectionFetch(`${kind}?${queueQuery(nextFilters, nextPage)}`, parse, {
        signal: attempt.signal,
      });
      if (attempt.signal.aborted) {
        return;
      }
      if (
        result.status !== 200 ||
        !result.data ||
        result.data.page !== nextPage ||
        result.data.pageSize !== nextFilters.pageSize
      ) {
        throw new Error("Queue unavailable");
      }
      setSnapshot(result.data);
      setRevision((value) => value + 1);
      setActive({ filters: nextFilters, page: nextPage });
      setDraft(nextFilters);
      window.history.replaceState(
        null,
        "",
        `${queuePath(kind)}?${queueQuery(nextFilters, nextPage)}`,
      );
      return true;
    } catch {
      if (!attempt.signal.aborted) {
        toast.error(
          "The queue could not be refreshed. The last confirmed rows and filters remain shown.",
        );
      }
    } finally {
      if (!attempt.signal.aborted) {
        setBusy(false);
      }
      controller.current = null;
    }
  }

  async function loadBranches(page: number) {
    if (controller.current) {
      return;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    try {
      const result = await intakeFetch(
        `/api/intake/branches?page=${page}`,
        (v) => parsePage(v, parseBranch),
        { signal: attempt.signal },
      );
      if (!attempt.signal.aborted && result.data) {
        setBranches(result.data);
      }
    } catch {
      if (!attempt.signal.aborted) {
        toast.error("Branch choices could not be refreshed. Existing choices remain shown.");
      }
    } finally {
      if (!attempt.signal.aborted) {
        setBusy(false);
      }
      controller.current = null;
    }
  }
  return { snapshot, active, draft, setDraft, branches, busy, ready, revision, load, loadBranches };
}
