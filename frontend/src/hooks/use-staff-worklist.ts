"use client";
import { useEffect, useRef, useState } from "react";
import { intakeFetch } from "@/lib/intake/browser";
import { parsePage, parseBranch } from "@/lib/intake/parse";
import { dateRangeError } from "@/lib/intake/list-query";
import { parseWorklistItem } from "@/lib/staff/parse";
import { staffQuery, staffViewQuery } from "@/lib/staff/query";
import type { ApiResult, Branch, Page } from "@/lib/intake/contracts";
import type { StaffFilters, WorklistItem } from "@/lib/staff/contracts";
import { useToast } from "@/components/ui/Toast";
import { useInitialToast } from "./use-initial-toast";

export function useStaffWorklist(
  initial: ApiResult<Page<WorklistItem>>,
  filters: StaffFilters,
  page: number,
  initialBranches: Page<Branch> | null,
) {
  const [snapshot, setSnapshot] = useState(initial.data);
  const [branches, setBranches] = useState(initialBranches);
  const [draft, setDraft] = useState(filters);
  const [active, setActive] = useState({ filters, page });
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

  async function load(nextFilters = active.filters, nextPage = active.page) {
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
      const result = await intakeFetch(
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
      setActive({ filters: nextFilters, page: nextPage });
      setDraft(nextFilters);
      const query = new URLSearchParams(staffViewQuery(nextFilters, nextPage));
      const selected = new URL(window.location.href).searchParams.get("appointment");
      if (selected) {
        query.set("appointment", selected);
      }
      window.history.replaceState(null, "", `/dashboard/intake?${query}`);
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
      const result = await intakeFetch(
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
  };
}
