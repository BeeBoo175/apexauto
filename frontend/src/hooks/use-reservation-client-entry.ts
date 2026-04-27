"use client";

import type { ReservationTable } from "@/components/vehicles/ReservationTable";
import { useStaffClientEntry } from "./use-staff-client-entry";
import { apiFetch, privateApiFetch } from "@/lib/api-browser";
import { parsePage, type ApiResult, type Page } from "@/lib/api";
import { isUuid } from "@/lib/auth/contracts";
import type { CurrentActor } from "@/lib/auth/actor";
import { parseBranch, type Branch } from "@/lib/branches";
import type { SearchParams } from "@/lib/query";
import {
  reservationParams,
  reservationQuery,
  type ReservationPage,
} from "@/lib/vehicles/reservations";
import { parseReservationSnapshot } from "@/lib/vehicles/parse-reservations";
import {
  parseReservationDetailSnapshot,
  type ReservationDetail,
} from "@/lib/vehicles/reservation-detail";

interface ReservationEntry extends ReturnType<typeof reservationParams> {
  Table: typeof ReservationTable;
  initial: ApiResult<ReservationPage>;
  branches: Page<Branch> | null;
  selected: ApiResult<ReservationDetail> | null;
  reservationId: string;
}

export function useReservationClientEntry(actor: CurrentActor, parameters: SearchParams) {
  return useStaffClientEntry<ReservationEntry>(async (signal) => {
    const query = reservationParams(parameters);
    const { filters, page } = query;
    const reservationId = isUuid(parameters.reservation) ? parameters.reservation : "";

    const [initial, branches, selected, { ReservationTable: Table }] = await Promise.all([
      privateApiFetch(
        `/api/staff/reservations?${reservationQuery(filters, page)}`,
        parseReservationSnapshot,
        { signal },
      ),
      actor.role === "GeneralManager"
        ? apiFetch("/api/intake/branches?page=1", (value) => parsePage(value, parseBranch), {
            signal,
          }).catch(() => null)
        : null,
      reservationId
        ? privateApiFetch(
            `/api/staff/reservations/${reservationId}`,
            (value) => {
              const detail = parseReservationDetailSnapshot(value);
              return detail?.reservation.id === reservationId ? detail : null;
            },
            { signal },
          ).catch(() => null)
        : null,
      import("@/components/vehicles/ReservationTable"),
    ]);
    if (
      initial.data &&
      (initial.data.page !== page ||
        initial.data.pageSize !== filters.pageSize ||
        initial.data.items.some((row) => (row.status === "Active") !== filters.isActive))
    ) {
      throw new Error("Reservations unavailable");
    }
    return {
      ...query,
      reservationId,
      Table,
      initial,
      branches: branches?.data ?? null,
      selected: reservationId
        ? (selected ?? { status: 502, data: null, message: "", uncertain: false })
        : null,
    };
  }, "Reservations could not be loaded. Retry to open the workspace.");
}
