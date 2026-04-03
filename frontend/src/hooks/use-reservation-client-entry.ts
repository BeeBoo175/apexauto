"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import type { ReservationTable } from "@/components/vehicles/ReservationTable";
import { useToast } from "@/components/ui/Toast";
import { apiFetch } from "@/lib/api-browser";
import { parsePage, type ApiResult, type Page } from "@/lib/api";
import { isUuid } from "@/lib/auth/contracts";
import type { CurrentActor } from "@/lib/auth/actor";
import { parseBranch, type Branch } from "@/lib/branches";
import type { SearchParams } from "@/lib/query";
import {
  reservationParams,
  reservationQuery,
  type ReservationItem,
} from "@/lib/vehicles/reservations";
import { parseReservationSnapshot } from "@/lib/vehicles/parse-reservations";
import {
  parseReservationDetailSnapshot,
  type ReservationDetail,
} from "@/lib/vehicles/reservation-detail";

interface ReservationEntry extends ReturnType<typeof reservationParams> {
  Table: typeof ReservationTable;
  initial: ApiResult<Page<ReservationItem>>;
  branches: Page<Branch> | null;
  selected: ApiResult<ReservationDetail> | null;
  reservationId: string;
}

export function useReservationClientEntry(actor: CurrentActor, parameters: SearchParams) {
  const [entry, setEntry] = useState<ReservationEntry | null>(null);
  const [failed, setFailed] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const { error } = useToast();

  const load = useCallback(async () => {
    if (controller.current) {
      return;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setFailed(false);
    const query = reservationParams(parameters);
    const { filters, page } = query;
    const reservationId = isUuid(parameters.reservation) ? parameters.reservation : "";

    try {
      const [initial, branches, selected, { ReservationTable: Table }] = await Promise.all([
        apiFetch(
          `/api/staff/reservations?${reservationQuery(filters, page)}`,
          parseReservationSnapshot,
          { signal: attempt.signal },
          { requireActor: true },
        ),
        actor.role === "GeneralManager"
          ? apiFetch("/api/intake/branches?page=1", (value) => parsePage(value, parseBranch), {
              signal: attempt.signal,
            }).catch(() => null)
          : null,
        reservationId
          ? apiFetch(
              `/api/staff/reservations/${reservationId}`,
              (value) => {
                const detail = parseReservationDetailSnapshot(value);
                return detail?.reservation.id === reservationId ? detail : null;
              },
              { signal: attempt.signal },
              { requireActor: true },
            ).catch(() => null)
          : null,
        import("@/components/vehicles/ReservationTable"),
      ]);
      if (attempt.signal.aborted || controller.current !== attempt) {
        return;
      }
      if (
        initial.data &&
        (initial.data.page !== page ||
          initial.data.pageSize !== filters.pageSize ||
          initial.data.items.some((row) => (row.status === "Active") !== filters.isActive))
      ) {
        throw new Error("Reservations unavailable");
      }
      setEntry({
        ...query,
        reservationId,
        Table,
        initial,
        branches: branches?.data ?? null,
        selected: reservationId
          ? (selected ?? { status: 502, data: null, message: "", uncertain: false })
          : null,
      });
    } catch {
      if (!attempt.signal.aborted && controller.current === attempt) {
        setFailed(true);
        error("Reservations could not be loaded. Retry to open the workspace.");
      }
    } finally {
      attempt.abort();
      if (controller.current === attempt) {
        controller.current = null;
      }
    }
  }, [actor, parameters, error]);

  useEffect(() => {
    let active = true;
    queueMicrotask(() => {
      if (active) {
        void load();
      }
    });
    return () => {
      active = false;
      controller.current?.abort();
      controller.current = null;
    };
  }, [load]);

  return { entry, failed, retry: load };
}
