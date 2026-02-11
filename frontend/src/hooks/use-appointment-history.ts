"use client";

import { useEffect, useRef, useState } from "react";
import { intakeFetch } from "@/lib/intake/browser";
import { parseAppointment, parsePage } from "@/lib/intake/parse";
import { dateRangeError, historyQuery, type HistoryFilters } from "@/lib/intake/list-query";
import type { ApiResult, Appointment, Page } from "@/lib/intake/contracts";
import { useToast } from "@/components/ui/Toast";
import { useInitialToast } from "./use-initial-toast";

export function useAppointmentHistory(
  initial: ApiResult<Page<Appointment>>,
  filters: HistoryFilters,
  page: number,
) {
  const [snapshot, setSnapshot] = useState(initial.data);
  const [active, setActive] = useState({ filters, page });
  const [draft, setDraft] = useState(filters);
  const [busy, setBusy] = useState(false);
  useInitialToast(initial.message);
  const controller = useRef<AbortController | null>(null);
  const { error } = useToast();
  useEffect(() => () => controller.current?.abort(), []);

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
        `/api/intake/appointments?${historyQuery(nextFilters, nextPage)}`,
        (value) => parsePage(value, parseAppointment),
        { signal: attempt.signal },
      );
      if (attempt.signal.aborted) {
        return;
      }
      if (result.status !== 200 || !result.data || result.data.page !== nextPage) {
        throw new Error("History unavailable");
      }
      setSnapshot(result.data);
      setActive({ filters: nextFilters, page: nextPage });
      window.history.replaceState(null, "", `/account?${historyQuery(nextFilters, nextPage)}`);
    } catch {
      if (!attempt.signal.aborted) {
        const notice =
          "Appointment history could not be refreshed. The last confirmed records and filters remain shown.";
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

  function cancelled(id: string, cancelledAt: string) {
    setSnapshot(
      (current) =>
        current && {
          ...current,
          items: current.items.map((item) => (item.id === id ? { ...item, cancelledAt } : item)),
        },
    );
    void load();
  }

  return { snapshot, active, draft, setDraft, busy, load, cancelled };
}
