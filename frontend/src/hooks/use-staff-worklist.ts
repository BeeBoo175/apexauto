"use client";
import { useEffect, useRef, useState } from "react";
import { apiFetch } from "@/lib/api-browser";
import { parsePage } from "@/lib/api";
import { parseBranch } from "@/lib/branches";
import { dateRangeError } from "@/lib/intake/list-query";
import { parseWorklistItem } from "@/lib/staff/parse";
import { staffQuery, staffViewQuery } from "@/lib/staff/query";
import type { ApiResult, Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import type { StaffFilters, WorklistItem } from "@/lib/staff/contracts";
import { useToast } from "@/components/ui/Toast";
import { useInitialToast } from "./use-initial-toast";

interface WorklistSort {
  column: string;
  direction: "asc" | "desc";
}

function initialSort(filters: StaffFilters): WorklistSort | null {
  if (filters.sort === "startsAt" && filters.direction === "asc") {
    return null;
  }
  return { column: filters.sort, direction: filters.direction };
}

export function useStaffWorklist(
  initial: ApiResult<Page<WorklistItem>>,
  filters: StaffFilters,
  page: number,
  initialBranches: Page<Branch> | null,
) {
  const [snapshot, setSnapshot] = useState(initial.data);
  const [branches, setBranches] = useState(initialBranches);
  const [draft, setDraft] = useState(filters);
  const [active, setActive] = useState({ filters, page, sort: initialSort(filters) });
  const [busy, setBusy] = useState(false);
  const [ready, setReady] = useState(false);
  useInitialToast(initial.message);
  const [revision, setRevision] = useState(0);
  const controller = useRef<AbortController | null>(null);
  const { error } = useToast();
  useEffect(() => {
    queueMicrotask(() => setReady(true));
    return () => controller.current?.abort();
  }, []);

  function cancel() {
    controller.current?.abort();
    controller.current = null;
    setBusy(false);
  }

  async function load(
    nextFilters = active.filters,
    nextPage = active.page,
    updateHistory = true,
    selectedSort?: WorklistSort | null,
  ) {
    if (controller.current) {
      return;
    }
    const invalid = dateRangeError(nextFilters);
    if (invalid) {
      error(invalid);
      return;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    try {
      const result = await apiFetch(
        `/api/staff/intake/appointments?${staffQuery(nextFilters, nextPage)}`,
        (value) => parsePage(value, parseWorklistItem),
        { signal: attempt.signal },
      );
      if (attempt.signal.aborted) {
        return;
      }
      if (result.status !== 200 || !result.data || result.data.page !== nextPage) {
        throw new Error("Worklist unavailable");
      }
      setSnapshot(result.data);
      setRevision((current) => current + 1);
      let sort = selectedSort;
      if (sort === undefined) {
        const sameOrder =
          nextFilters.sort === active.filters.sort &&
          nextFilters.direction === active.filters.direction;
        sort = sameOrder ? active.sort : initialSort(nextFilters);
      }
      setActive({ filters: nextFilters, page: nextPage, sort });
      setDraft(nextFilters);
      const query = new URLSearchParams(staffViewQuery(nextFilters, nextPage));
      const selected = new URL(window.location.href).searchParams.get("appointment");
      if (selected) {
        query.set("appointment", selected);
      }
      if (updateHistory) {
        window.history.replaceState(null, "", `/dashboard/intake?${query}`);
      }
      return true;
    } catch {
      if (!attempt.signal.aborted) {
        const notice =
          "The worklist could not be refreshed. The last confirmed rows and filters remain shown.";
        error(notice);
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
        (value) => parsePage(value, parseBranch),
        { signal: attempt.signal },
      );
      if (attempt.signal.aborted) {
        return;
      }
      if (result.status !== 200 || !result.data) {
        throw new Error("Branches unavailable");
      }
      setBranches(result.data);
    } catch {
      if (!attempt.signal.aborted) {
        error("Branch choices could not be refreshed. Existing choices remain shown.");
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
    branches,
    draft,
    setDraft,
    active,
    busy,
    ready,
    revision,
    load,
    loadBranches,
    cancel,
  };
}
