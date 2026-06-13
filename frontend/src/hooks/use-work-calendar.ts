"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import type { CurrentActor } from "@/lib/auth/actor";
import type { WorkCalendarFilters } from "@/lib/staff/work-calendar";
import { useInitialToast } from "./use-initial-toast";
import {
  readCalendarBranches,
  readScheduledWork,
  readUnscheduledWork,
  type WorkCalendarEntry,
} from "./use-work-calendar-entry";

export function useWorkCalendar(actor: CurrentActor, initial: WorkCalendarEntry) {
  const [filters, setFilters] = useState(initial.filters);
  const [scheduled, setScheduled] = useState({
    data: initial.scheduled?.data ?? null,
    filters: initial.filters,
  });
  const [unscheduled, setUnscheduled] = useState({
    data: initial.unscheduled?.data ?? null,
    filters: initial.filters,
  });
  const [branches, setBranches] = useState(initial.branches);
  const [pending, setPending] = useState({ scheduled: false, unscheduled: false, branches: false });
  const controllers = useRef<{
    scheduled?: AbortController;
    unscheduled?: AbortController;
    branches?: AbortController;
  }>({});
  const toast = useToast();
  useInitialToast(
    initial.scheduled && !initial.scheduled.data ? calendarFailure(initial.scheduled.status) : null,
  );
  useInitialToast(
    initial.unscheduled && !initial.unscheduled.data
      ? "Ongoing work could not be loaded. Use Retry ongoing work."
      : null,
  );
  useEffect(
    () => () => {
      Object.values(controllers.current).forEach((controller) => controller?.abort());
    },
    [],
  );

  async function loadScheduled(next = filters) {
    controllers.current.scheduled?.abort();
    const attempt = new AbortController();
    controllers.current.scheduled = attempt;
    setPending((current) => ({ ...current, scheduled: true }));
    try {
      const result = await readScheduledWork(actor, next, attempt.signal);
      if (attempt.signal.aborted) {
        return;
      }
      if (!result.data) {
        toast.error(calendarFailure(result.status));
        return;
      }
      setScheduled({ data: result.data, filters: next });
      setFilters((current) => ({ ...current, date: next.date, assignment: next.assignment }));
    } catch {
      if (!attempt.signal.aborted) {
        toast.error(calendarFailure(502));
      }
    } finally {
      if (controllers.current.scheduled === attempt) {
        delete controllers.current.scheduled;
        setPending((current) => ({ ...current, scheduled: false }));
      }
    }
  }

  async function loadUnscheduled(next = filters) {
    controllers.current.unscheduled?.abort();
    const attempt = new AbortController();
    controllers.current.unscheduled = attempt;
    setPending((current) => ({ ...current, unscheduled: true }));
    try {
      const result = await readUnscheduledWork(actor, next, attempt.signal);
      if (attempt.signal.aborted) {
        return;
      }
      if (!result.data) {
        throw new Error("Ongoing work unavailable");
      }
      setUnscheduled({ data: result.data, filters: next });
      setFilters((current) => ({
        ...current,
        kind: next.kind,
        page: next.page,
        pageSize: next.pageSize,
      }));
    } catch {
      if (!attempt.signal.aborted) {
        toast.error("Ongoing work could not be refreshed. Confirmed work remains shown.");
      }
    } finally {
      if (controllers.current.unscheduled === attempt) {
        delete controllers.current.unscheduled;
        setPending((current) => ({ ...current, unscheduled: false }));
      }
    }
  }

  async function changeScope(next: WorkCalendarFilters) {
    controllers.current.scheduled?.abort();
    controllers.current.unscheduled?.abort();
    const confirmed = { ...next, search: next.search.trim() };
    const attempt = new AbortController();
    controllers.current.scheduled = attempt;
    controllers.current.unscheduled = attempt;
    setPending((current) => ({ ...current, scheduled: true, unscheduled: true }));
    try {
      const [appointments, work] = await Promise.all([
        readScheduledWork(actor, confirmed, attempt.signal),
        readUnscheduledWork(actor, confirmed, attempt.signal),
      ]);
      if (attempt.signal.aborted) {
        return false;
      }
      if (!appointments.data || !work.data) {
        if (!scheduled.data && !unscheduled.data) {
          // With no confirmed content to preserve, expose the chosen scope's narrowing and retry controls.
          setFilters(confirmed);
          setScheduled({ data: appointments.data, filters: confirmed });
          setUnscheduled({ data: work.data, filters: confirmed });
        }
        toast.error(
          appointments.data
            ? "Ongoing work could not be loaded. Confirmed content and filters remain shown; retry or change the shared filters."
            : calendarFailure(appointments.status),
        );
        return false;
      }
      // A shared scope commits both panels together so their controls never describe different reads.
      setFilters(confirmed);
      setScheduled({ data: appointments.data, filters: confirmed });
      setUnscheduled({ data: work.data, filters: confirmed });
      return true;
    } catch {
      if (!attempt.signal.aborted) {
        if (!scheduled.data && !unscheduled.data) {
          setFilters(confirmed);
          setScheduled({ data: null, filters: confirmed });
          setUnscheduled({ data: null, filters: confirmed });
        }
        toast.error(
          "The shared filters could not be applied. Confirmed content remains shown; retry or change the filters.",
        );
        attempt.abort();
      }
      return false;
    } finally {
      if (controllers.current.scheduled === attempt) {
        delete controllers.current.scheduled;
        delete controllers.current.unscheduled;
        setPending((current) => ({ ...current, scheduled: false, unscheduled: false }));
      }
    }
  }

  async function loadBranches(page: number) {
    controllers.current.branches?.abort();
    const attempt = new AbortController();
    controllers.current.branches = attempt;
    setPending((current) => ({ ...current, branches: true }));
    try {
      const result = await readCalendarBranches(page, attempt.signal);
      if (attempt.signal.aborted) {
        return;
      }
      if (!result.data) {
        throw new Error("Branches unavailable");
      }
      setBranches(result.data);
    } catch {
      if (!attempt.signal.aborted) {
        toast.error("Branch choices could not be loaded. Existing choices remain available.");
      }
    } finally {
      if (controllers.current.branches === attempt) {
        delete controllers.current.branches;
        setPending((current) => ({ ...current, branches: false }));
      }
    }
  }

  return {
    filters,
    scheduled,
    unscheduled,
    branches,
    pending,
    changeScope,
    changeWork: (kind: WorkCalendarFilters["kind"]) =>
      loadUnscheduled({ ...filters, kind, page: 1 }),
    changeMonth: (date: string) => loadScheduled({ ...filters, date }),
    changeAssignment: (assignment: WorkCalendarFilters["assignment"]) =>
      loadScheduled({ ...filters, assignment }),
    changePage: (page: number, pageSize: number) => loadUnscheduled({ ...filters, page, pageSize }),
    refresh: () => changeScope(filters),
    loadScheduled,
    loadUnscheduled,
    loadBranches,
  };
}

function calendarFailure(status: number) {
  if (status === 422) {
    return "More than 1,000 appointments and test drives match. Refine the shared search, assignment or month. Confirmed content and filters remain shown.";
  }
  return "Appointments could not be refreshed. Confirmed content and filters remain shown; retry or change the filters.";
}
