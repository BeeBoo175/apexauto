import "server-only";
import { API_DEADLINE_MS } from "@/lib/api";

import { readCurrentActor } from "@/lib/auth/server";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { HttpError, readBoundedJson } from "@/lib/http";
import { requireSameOrigin } from "@/lib/server/http/request";
import {
  apiError,
  apiJson,
  privateApiRead,
  requestApiJson,
  sessionToken,
} from "@/lib/server/http/api";
import { dateRangeError } from "@/lib/intake/list-query";
import {
  accountReservationQuery,
  parseAccountReservations,
  type ReservationFilters,
} from "./account-reservations";
import {
  matchesCustomerCancellation,
  parseCustomerCancellation,
} from "./customer-reservation-cancellation";

export async function readAccountReservations(
  page: number,
  filters: ReservationFilters,
  signal?: AbortSignal,
) {
  return privateApiRead(
    `/api/account/reservations?${accountReservationQuery(page, filters)}`,
    (value) => {
      const snapshot = parseAccountReservations(value);
      return snapshot?.page === page ? snapshot : null;
    },
    signal,
  );
}

export async function relayAccountReservations(request: Request) {
  try {
    const query = new URL(request.url).searchParams;
    const page = Number(query.get("page") ?? 1);
    if (!Number.isSafeInteger(page) || page < 1 || page > 2147483647) {
      throw new HttpError(400, "Choose a valid reservation page.");
    }
    const filters = {
      from: query.get("from") ?? "",
      to: query.get("to") ?? "",
      search: query.get("search") ?? "",
    };
    if (filters.search.length > 160) {
      throw new HttpError(400, "Search with at most 160 characters.");
    }
    if (dateRangeError(filters)) {
      throw new HttpError(400, dateRangeError(filters));
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const current = await readCurrentActor(signal);
    if (!current.actor) {
      throw new HttpError(
        current.status === 401 ? 401 : 502,
        "Your session could not be confirmed.",
      );
    }
    const result = await readAccountReservations(page, filters, signal);
    if (result.data && result.data.actor.userId !== current.actor.userId) {
      throw new HttpError(502, "Your account response could not be confirmed.");
    }
    return apiJson({ ...result, actor: current.actor });
  } catch (error) {
    return apiError(error);
  }
}

export async function relayCustomerCancellation(request: Request, id: string) {
  try {
    requireSameOrigin(request);
    if (!isUuid(id)) {
      throw new HttpError(400, "Choose a valid reservation.");
    }
    if (
      request.headers.get("content-type")?.split(";")[0].trim().toLowerCase() !== "application/json"
    ) {
      throw new HttpError(415, "Send application/json.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const body = await readBoundedJson(request.body, signal, 8 * 1024);
    if (!isRecord(body) || !isUuid(body.operationId) || body.reason !== null) {
      throw new HttpError(400, "Provide a cancellation operation ID without a staff reason.");
    }
    const current = await readCurrentActor(signal);
    if (!current.actor) {
      throw new HttpError(
        current.status === 401 ? 401 : 502,
        "Your session could not be confirmed.",
      );
    }
    if (current.actor.role !== "Customer") {
      return apiJson({
        status: 403,
        data: null,
        message: "Customer account required.",
        uncertain: false,
        actor: current.actor,
      });
    }
    const result = await requestApiJson(
      `/api/reservations/${id}/cancellations`,
      parseCustomerCancellation,
      {
        token: await sessionToken(),
        signal,
        method: "POST",
        body: { operationId: body.operationId, reason: null },
      },
    );
    if (result.data && !matchesCustomerCancellation(result.data, body, id, current.actor.userId)) {
      throw new HttpError(
        502,
        "The cancellation receipt could not be confirmed. Reconcile the same request.",
      );
    }
    return apiJson({ ...result, actor: current.actor });
  } catch (error) {
    return apiError(
      error instanceof SyntaxError ? new HttpError(400, "Provide valid JSON details.") : error,
    );
  }
}
