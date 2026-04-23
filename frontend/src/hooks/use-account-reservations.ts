"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { privateApiFetch } from "@/lib/api-browser";
import type { ApiResult } from "@/lib/api";
import {
  parseAccountReservations,
  type AccountReservation,
  type AccountReservations,
} from "@/lib/vehicles/account-reservations";
import type { CustomerReservationCancellation } from "@/lib/vehicles/customer-reservation-cancellation";
import { useInitialToast } from "./use-initial-toast";

export function useAccountReservations(
  initial: ApiResult<AccountReservations>,
  initialPage: number,
) {
  const [snapshot, setSnapshot] = useState(initial.data);
  const [page, setPage] = useState(initialPage);
  const [busy, setBusy] = useState(false);
  const [failed, setFailed] = useState(initial.status !== 200);
  const [released, setReleased] = useState<AccountReservation | null>(null);
  const controller = useRef<AbortController | null>(null);
  const toast = useToast();
  useInitialToast(initial.message);
  useEffect(() => () => controller.current?.abort(), []);

  async function load(nextPage = page) {
    controller.current?.abort();
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    try {
      const result = await privateApiFetch(
        `/api/account/reservations?page=${nextPage}`,
        parseAccountReservations,
        { signal: attempt.signal },
      );
      if (attempt.signal.aborted) {
        return;
      }
      if (result.status !== 200 || !result.data || result.data.page !== nextPage) {
        throw new Error("Reservation history unavailable");
      }
      setSnapshot(result.data);
      setPage(nextPage);
      setFailed(false);
      const url = new URL(window.location.href);
      url.searchParams.set("reservationPage", String(nextPage));
      window.history.replaceState(null, "", `${url.pathname}${url.search}${url.hash}`);
    } catch {
      if (!attempt.signal.aborted) {
        setFailed(true);
        toast.error(
          "Reservations could not be refreshed. Your last confirmed records remain shown.",
        );
      }
    } finally {
      if (controller.current === attempt) {
        controller.current = null;
        setBusy(false);
      }
    }
  }

  function cancelled(item: AccountReservation, receipt: CustomerReservationCancellation) {
    const confirmed = { ...item, isActive: false, release: receipt };
    setReleased(confirmed);
    setSnapshot(
      (current) =>
        current && {
          ...current,
          active: current.active?.receipt.id === item.receipt.id ? null : current.active,
          items: current.items.map((row) => (row.receipt.id === item.receipt.id ? confirmed : row)),
        },
    );
    void load();
  }

  return { snapshot, page, busy, failed, released, load, cancelled };
}
