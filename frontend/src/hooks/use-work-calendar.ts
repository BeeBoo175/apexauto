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
  const branchRequired = actor.role === "GeneralManager" && !filters.branchId;
  useInitialToast(
    initial.scheduled && !initial.scheduled.data ? calendarFailure(initial.scheduled.status) : null,
  );
  useInitialToast(
    initial.unscheduled && !initial.unscheduled.data
      ? "Unscheduled work could not be loaded. Use Retry unscheduled work."
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
        throw new Error("Unscheduled work unavailable");
      }
      setUnscheduled({ data: result.data, filters: next });
    } catch {
      if (!attempt.signal.aborted) {
        toast.error("Unscheduled work could not be refreshed. Confirmed work remains shown.");
      }
    } finally {
      if (controllers.current.unscheduled === attempt) {
        delete controllers.current.unscheduled;
        setPending((current) => ({ ...current, unscheduled: false }));
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

  async function changeScope(next: WorkCalendarFilters) {
    const nextFilters = { ...next, employee: next.employee.trim(), page: 1 };
    setFilters(nextFilters);
    if (actor.role === "GeneralManager" && !nextFilters.branchId) {
      controllers.current.scheduled?.abort();
      controllers.current.unscheduled?.abort();
      setScheduled({ data: null, filters: nextFilters });
      setUnscheduled({ data: null, filters: nextFilters });
      return;
    }
    await Promise.all([loadScheduled(nextFilters), loadUnscheduled(nextFilters)]);
  }

  async function changeMonth(date: string) {
    const next = { ...filters, date };
    setFilters(next);
    await loadScheduled(next);
  }

  async function changePage(page: number, pageSize: number) {
    const next = { ...filters, page, pageSize };
    setFilters(next);
    await loadUnscheduled(next);
  }

  async function refresh() {
    if (!branchRequired) {
      await Promise.all([loadScheduled(filters), loadUnscheduled(filters)]);
    }
  }

  return {
    filters,
    scheduled,
    unscheduled,
    branches,
    pending,
    branchRequired,
    changeScope,
    changeMonth,
    changePage,
    refresh,
    loadScheduled,
    loadUnscheduled,
    loadBranches,
  };
}

function calendarFailure(status: number) {
  if (status === 422) {
    return "More than 1,000 appointments match. Filter by employee or choose another month. Confirmed appointments remain shown.";
  }
  return "Scheduled work could not be refreshed. Confirmed appointments remain shown; retry or change the filters.";
}
