"use client";
import { useEffect, useRef, useState } from "react";
import { inspectionFetch } from "@/lib/inspection/browser";
import { dateRangeError } from "@/lib/intake/list-query";
import type { ApiResult, Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import { parseBranch } from "@/lib/branches";
import { parsePage } from "@/lib/api";
import { apiFetch } from "@/lib/api-browser";
import type { QueueFilters, QueueKind } from "@/lib/inspection/contracts";
import { queueLocationQuery, queuePath, queueQuery } from "@/lib/inspection/query";
import { useToast } from "@/components/ui/Toast";
import { useInitialToast } from "./use-initial-toast";
import type { TableSort } from "@/components/ui/data-table/types";

function initialSort(filters: QueueFilters): TableSort | null {
  const column = filters.sort ?? "since";
  const direction = filters.direction ?? "desc";
  if (column === "since" && direction === "desc") {
    return null;
  }
  return { column, direction };
}

export function useInspectionQueue<T>(
  kind: QueueKind,
  initial: ApiResult<Page<T>>,
  filters: QueueFilters,
  page: number,
  initialBranches: Page<Branch> | null,
  parse: (v: unknown) => Page<T> | null,
) {
  const [snapshot, setSnapshot] = useState(initial.data);
  const [active, setActive] = useState({ filters, page, sort: initialSort(filters) });
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
      setActive({ filters, page, sort: initialSort(filters) });
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

  function refresh(notifyFailure = true) {
    controller.current?.abort();
    controller.current = null;
    return load(active.filters, active.page, undefined, notifyFailure);
  }

  async function load(
    nextFilters = active.filters,
    nextPage = active.page,
    selectedSort?: TableSort | null,
    notifyFailure = true,
  ) {
    if (controller.current) {
      return;
    }
    const invalid = dateRangeError(nextFilters);
    if (invalid) {
      toast.error(invalid);
      return;
    }
    const attempt = new AbortController();
    const submittedDraft = draft;
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
      let sort = selectedSort;
      if (sort === undefined) {
        const sameOrder =
          nextFilters.sort === active.filters.sort &&
          nextFilters.direction === active.filters.direction;
        sort = sameOrder ? active.sort : initialSort(nextFilters);
      }
      setActive({ filters: nextFilters, page: nextPage, sort });
      setDraft((current) => {
        if (current === submittedDraft) {
          return nextFilters;
        }
        return { ...current, sort: nextFilters.sort, direction: nextFilters.direction };
      });
      const current = new URL(window.location.href);
      const query = queueLocationQuery(current.searchParams, nextFilters, nextPage);
      window.history.replaceState(null, "", `${queuePath(kind)}?${query}`);
      return true;
    } catch {
      if (!attempt.signal.aborted && notifyFailure) {
        toast.error(
          "The queue could not be refreshed. The last confirmed rows and filters remain shown.",
        );
      }
      return false;
    } finally {
      if (!attempt.signal.aborted) {
        setBusy(false);
      }
      if (controller.current === attempt) {
        controller.current = null;
      }
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
      const result = await apiFetch(
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
      if (controller.current === attempt) {
        controller.current = null;
      }
    }
  }
  return {
    snapshot,
    active,
    draft,
    setDraft,
    branches,
    busy,
    ready,
    revision,
    load,
    loadBranches,
    refresh,
  };
}
