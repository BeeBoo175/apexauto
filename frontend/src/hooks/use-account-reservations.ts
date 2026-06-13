"use client";

import type { ApiResult } from "@/lib/api";
import {
  accountReservationQuery,
  parseAccountReservations,
  type ReservationFilters,
  type AccountReservation,
  type AccountReservations,
} from "@/lib/vehicles/account-reservations";
import type { CustomerReservationCancellation } from "@/lib/vehicles/customer-reservation-cancellation";
import { useAccountHistory } from "./use-account-history";

export function useAccountReservations(
  initial: ApiResult<AccountReservations>,
  initialPage: number,
  initialFilters: ReservationFilters,
) {
  const history = useAccountHistory(initial, initialPage, initialFilters, {
    path: (filters, page) => `/api/account/reservations?${accountReservationQuery(page, filters)}`,
    parse: parseAccountReservations,
    prefix: "reservation",
    failureMessage:
      "Reservations could not be refreshed. Your last confirmed records remain shown.",
  });

  function cancelled(item: AccountReservation, receipt: CustomerReservationCancellation) {
    const confirmed = { ...item, isActive: false, release: receipt };
    history.setSnapshot(
      (current) =>
        current && {
          ...current,
          active: current.active?.receipt.id === item.receipt.id ? null : current.active,
          items: current.items.map((row) => (row.receipt.id === item.receipt.id ? confirmed : row)),
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
