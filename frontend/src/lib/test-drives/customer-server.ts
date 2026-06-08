import "server-only";
import { API_DEADLINE_MS } from "@/lib/api";
import { actorIdentity } from "@/lib/auth/actor";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { HttpError, readBoundedJson } from "@/lib/http";
import { dateRangeError } from "@/lib/intake/list-query";
import { requireSameOrigin } from "@/lib/server/http/request";
import {
  apiError,
  apiJson,
  privateApiRead,
  requestApiJson,
  sessionToken,
} from "@/lib/server/http/api";
import { accessDenied, confirmedActor, requireJsonRequest } from "@/lib/server/http/staff-relay";
import {
  parseTestDriveBooking,
  parseTestDriveCancellation,
  parseTestDriveHistory,
  parseTestDriveRequest,
  parseTestDriveSlots,
  matchesTestDriveBooking,
} from "./customer";
import { testDriveHistoryQuery, type TestDriveHistoryFilters } from "./account";
import { parseTestDriveConflict } from "./errors";

export async function relayTestDriveSlots(request: Request, vehicleId: string) {
  try {
    const date = new URL(request.url).searchParams.get("date");
    if (!isUuid(vehicleId) || !date || !/^\d{4}-\d{2}-\d{2}$/.test(date)) {
      throw new HttpError(400, "Choose a vehicle and a Malaysian booking date.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const result = await requestApiJson(
      `/api/vehicles/${vehicleId}/test-drive-slots?date=${date}`,
      (value) => {
        const offer = parseTestDriveSlots(value);
        return offer?.vehicleId === vehicleId && offer.date === date ? offer : null;
      },
      { signal },
    );
    return apiJson(result);
  } catch (error) {
    return apiError(error);
  }
}

export async function readTestDriveHistory(
  page: number,
  filters: TestDriveHistoryFilters,
  signal?: AbortSignal,
) {
  const query = testDriveHistoryQuery(filters, page);
  return privateApiRead(
    `/api/account/test-drives?${query}`,
    (value) => {
      const snapshot = parseTestDriveHistory(value);
      return snapshot?.page === page ? snapshot : null;
    },
    signal,
  );
}

export async function relayTestDriveHistory(request: Request) {
  try {
    const query = new URL(request.url).searchParams;
    const page = Number(query.get("page") ?? 1);
    const filters = {
      from: query.get("from") ?? "",
      to: query.get("to") ?? "",
      search: query.get("search")?.trim() ?? "",
    };
    if (filters.search.length > 160) {
      throw new HttpError(400, "Search must be at most 160 characters.");
    }
    if (!Number.isSafeInteger(page) || page < 1 || page > 2147483647) {
      throw new HttpError(400, "Choose a valid test-drive history page.");
    }
    const dateError = dateRangeError(filters);
    if (dateError) {
      throw new HttpError(400, dateError);
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const actor = await confirmedActor(signal);
    if (actor.role !== "Customer") {
      return accessDenied(actor, "Customer account required.");
    }
    const result = await readTestDriveHistory(page, filters, signal);
    if (result.data && actorIdentity(result.data.actor) !== actorIdentity(actor)) {
      throw new HttpError(502, "Your account response could not be confirmed.");
    }
    return apiJson({ ...result, actor });
  } catch (error) {
    return apiError(error);
  }
}

export async function relayTestDriveBooking(request: Request, vehicleId: string) {
  try {
    requireSameOrigin(request);
    requireJsonRequest(request);
    if (!isUuid(vehicleId)) {
      throw new HttpError(400, "Choose a valid vehicle.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const body = parseTestDriveRequest(await readBoundedJson(request.body, signal, 8 * 1024));
    if (!body) {
      throw new HttpError(400, "Provide an operation ID, publication and booked start.");
    }
    const actor = await confirmedActor(signal);
    if (actor.role !== "Customer") {
      return accessDenied(actor, "Customer account required.");
    }
    const result = await requestApiJson(
      `/api/vehicles/${vehicleId}/test-drives`,
      (value) => {
        const receipt = parseTestDriveBooking(value);
        return receipt && matchesTestDriveBooking(receipt, body, vehicleId, actor.userId)
          ? receipt
          : null;
      },
      { token: await sessionToken(), signal, method: "POST", body },
      API_DEADLINE_MS,
      undefined,
      parseTestDriveConflict,
    );
    return apiJson({ ...result, actor });
  } catch (error) {
    return apiError(
      error instanceof SyntaxError ? new HttpError(400, "Provide valid JSON details.") : error,
    );
  }
}

export async function relayTestDriveCancellation(request: Request, bookingId: string) {
  try {
    requireSameOrigin(request);
    requireJsonRequest(request);
    if (!isUuid(bookingId)) {
      throw new HttpError(400, "Choose a valid test drive.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const value = await readBoundedJson(request.body, signal, 8 * 1024);
    if (
      !isRecord(value) ||
      !isUuid(value.operationId) ||
      (value.reason !== undefined && value.reason !== null)
    ) {
      throw new HttpError(400, "Provide a cancellation operation ID without a staff reason.");
    }
    const body = { operationId: value.operationId };
    const actor = await confirmedActor(signal);
    if (actor.role !== "Customer") {
      return accessDenied(actor, "Customer account required.");
    }
    const result = await requestApiJson(
      `/api/test-drives/${bookingId}/cancellations`,
      (raw) => {
        const receipt = parseTestDriveCancellation(raw);
        return receipt?.id === body.operationId &&
          receipt.bookingId === bookingId &&
          receipt.customerId === actor.userId &&
          receipt.kind === "CustomerCancelled"
          ? receipt
          : null;
      },
      { token: await sessionToken(), signal, method: "POST", body },
      API_DEADLINE_MS,
      undefined,
      parseTestDriveConflict,
    );
    return apiJson({ ...result, actor });
  } catch (error) {
    return apiError(
      error instanceof SyntaxError ? new HttpError(400, "Provide valid JSON details.") : error,
    );
  }
}
