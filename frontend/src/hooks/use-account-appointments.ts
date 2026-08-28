"use client";

import { parsePage } from "@/lib/api";
import { parseAppointment } from "@/lib/intake/parse";
import { historyQuery, type HistoryFilters } from "@/lib/intake/list-query";
import { ACCOUNT_PAGE_SIZE } from "@/lib/pagination";
import { useAccountHistory } from "./use-account-history";

export function useAccountAppointments(initialPage: number, initialFilters: HistoryFilters) {
  const history = useAccountHistory(initialPage, initialFilters, {
    path: (filters, page) =>
      `/api/intake/appointments?${historyQuery(filters, page, ACCOUNT_PAGE_SIZE)}`,
    parse: (value) => parsePage(value, parseAppointment, ACCOUNT_PAGE_SIZE),
    loadingLabel: "Loading appointments",
    failureMessage:
      "Appointment history could not be refreshed. The last confirmed records and filters remain shown.",
  });

  function cancelled(id: string, cancelledAt: string) {
    history.abort();
    history.setSnapshot(
      (current) =>
        current && {
          ...current,
          items: current.items.map((item) => (item.id === id ? { ...item, cancelledAt } : item)),
        },
    );
    void history.load();
  }

  return {
    snapshot: history.snapshot,
    page: history.page,
    filters: history.filters,
    draft: history.draft,
    setDraft: history.setDraft,
    busy: history.busy,
    load: history.load,
    cancelled,
  };
}
