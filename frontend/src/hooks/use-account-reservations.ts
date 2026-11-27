"use client";

import {
  accountReservationQuery,
  parseAccountReservations,
  type ReservationFilters,
  type AccountReservation,
} from "@/lib/vehicles/account-reservations";
import type { CustomerReservationCancellation } from "@/lib/vehicles/customer-reservation-cancellation";
import { useAccountHistory } from "./use-account-history";

export function useAccountReservations(
  initialPage: number,
  initialFilters: ReservationFilters,
  actorId: string,
) {
  const history = useAccountHistory(initialPage, initialFilters, {
    path: (filters, page) => `/api/account/reservations?${accountReservationQuery(page, filters)}`,
    parse: parseAccountReservations,
    confirms: (confirmed) => confirmed.actor.userId === actorId,
    loadingLabel: "Loading reservations",
    subject: "Your reservations",
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
