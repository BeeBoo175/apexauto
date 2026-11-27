"use client";

import {
  accountReservationQuery,
  parseAccountReservations,
  type ReservationFilters,
} from "@/lib/vehicles/account-reservations";
import { useAccountHistory } from "./use-account-history";

export function useAccountCollectionHistory(
  initialPage: number,
  initialFilters: ReservationFilters,
  actorId: string,
) {
  return useAccountHistory(initialPage, initialFilters, {
    path: (filters, page) =>
      `/api/account/reservations?${accountReservationQuery(page, filters, "collected")}`,
    parse: parseAccountReservations,
    confirms: (confirmed) =>
      confirmed.actor.userId === actorId && confirmed.items.every((item) => item.deliveredAt),
    loadingLabel: "Loading collection history",
    subject: "Your collection history",
  });
}
