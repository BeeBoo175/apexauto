"use client";

import { useEffect, useRef, useState } from "react";
import { privateApiFetch } from "@/lib/api-browser";
import { parseAppointment } from "@/lib/intake/parse";
import { parsePage } from "@/lib/api";
import { ACCOUNT_PAGE_SIZE } from "@/lib/pagination";
import { dateRangeError, historyQuery, type HistoryFilters } from "@/lib/intake/list-query";
import type { ApiResult, Page } from "@/lib/api";
import type { Appointment } from "@/lib/intake/contracts";
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
      const result = await privateApiFetch(
        `/api/intake/appointments?${historyQuery(nextFilters, nextPage, ACCOUNT_PAGE_SIZE)}`,
        (value) => parsePage(value, parseAppointment, ACCOUNT_PAGE_SIZE),
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
      const url = new URL(window.location.href);
      const query = new URLSearchParams(historyQuery(nextFilters, nextPage, ACCOUNT_PAGE_SIZE));
      for (const key of ["page", "pageSize", "from", "to"]) {
        url.searchParams.delete(key);
      }
      query.forEach((value, key) => url.searchParams.set(key, value));
      window.history.replaceState(null, "", `${url.pathname}${url.search}${url.hash}`);
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
