"use client";

import { useEffect, useRef, useState } from "react";
import { apiFetch } from "@/lib/api-browser";
import { parsePage } from "@/lib/api";
import { parseBranch } from "@/lib/branches";
import type { ApiResult, Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import {
  calendarHref,
  calendarQuery,
  calendarReadRange,
  parseCalendar,
  type CalendarFilters,
  type CalendarSnapshot,
} from "@/lib/staff/calendar";
import { isCalendarDate } from "@/lib/calendar";
import { useToast } from "@/components/ui/Toast";
import { useInitialToast } from "./use-initial-toast";

export function useStaffCalendar(
  initial: ApiResult<CalendarSnapshot>,
  filters: CalendarFilters,
  initialBranches: Page<Branch> | null,
) {
  const [snapshot, setSnapshot] = useState(initial.data);
  const [active, setActive] = useState(filters);
  const [branches, setBranches] = useState(initialBranches);
  const [ready, setReady] = useState(false);
  const [busy, setBusy] = useState(false);
  useInitialToast(initial.message);
  const controller = useRef<AbortController | null>(null);
  const toast = useToast();
  useEffect(() => {
    queueMicrotask(() => setReady(true));
    return () => controller.current?.abort();
  }, []);

  function cancel() {
    controller.current?.abort();
    controller.current = null;
    setBusy(false);
  }

  async function load(next = active, updateHistory = true) {
    if (controller.current || !isCalendarDate(next.date)) {
      return;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    try {
      const response = await apiFetch(
        `/api/staff/intake/calendar?${calendarQuery(next)}`,
        parseCalendar,
        { signal: attempt.signal },
      );
      if (attempt.signal.aborted) {
        return;
      }
      const range = calendarReadRange(next);
      if (
        response.status !== 200 ||
        !response.data ||
        response.data.from !== range.from ||
        response.data.to !== range.to
      ) {
        throw new Error(
          response.status === 422
            ? "More than 1,000 appointments match. Filter by branch or status."
            : "Calendar unavailable",
        );
      }
      setSnapshot(response.data);
      setActive(next);
      const url = new URL(calendarHref(next), window.location.origin);
      const selected = new URL(window.location.href).searchParams.get("appointment");
      if (selected) {
        url.searchParams.set("appointment", selected);
      }
      if (updateHistory) {
        window.history.replaceState(null, "", url);
      }
      return true;
    } catch (error) {
      if (!attempt.signal.aborted) {
        const reason =
          error instanceof Error && error.message.startsWith("More than")
            ? `${error.message} `
            : "";
        const notice = `${reason}The calendar could not be refreshed. The last confirmed month and appointments remain shown.`;
        toast.error(notice);
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
      const response = await apiFetch(
        `/api/intake/branches?page=${page}`,
        (value) => parsePage(value, parseBranch),
        { signal: attempt.signal },
      );
      if (attempt.signal.aborted) {
        return;
      }
      if (response.status !== 200 || !response.data) {
        throw new Error("Branch choices unavailable");
      }
      setBranches(response.data);
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
  return { snapshot, active, branches, ready, busy, load, loadBranches, cancel };
}
