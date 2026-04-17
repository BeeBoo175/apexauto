import "server-only";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { readCurrentActor } from "@/lib/auth/server";
import { HttpError, readBoundedJson } from "@/lib/http";
import { requireSameOrigin } from "@/lib/server/http/request";
import {
  apiError,
  apiJson,
  privateApiRead,
  requestApiJson,
  sessionToken,
} from "@/lib/server/http/api";
import { hasReservationAccess } from "./reservations";
import {
  parseReservationCancellation,
  reservationCancellationReason,
} from "./reservation-cancellation";

async function reservationActor(signal: AbortSignal) {
  const current = await readCurrentActor(signal);
  if (!current.actor) {
    throw new HttpError(
      current.status === 401 ? 401 : 502,
      "Your session could not be confirmed.",
    );
  }
  const denied = !hasReservationAccess(current.actor)
    ? apiJson({
        status: 403,
        data: null,
        message: "Reservation access unavailable.",
        uncertain: false,
        actor: current.actor,
      })
    : null;
  return { actor: current.actor, denied };
}

export async function relayReservationRead<T>(
  request: Request,
  resource: string,
  parse: (value: unknown) => T | null,
  matches?: (value: T) => boolean,
) {
  try {
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(25_000)]);
    const current = await reservationActor(signal);
    if (current.denied) {
      return current.denied;
    }
    const result = await privateApiRead(`/api/staff/reservations${resource}`, parse, signal);
    if (result.data && matches && !matches(result.data)) {
      throw new HttpError(502, "The reservation response could not be confirmed.");
    }
    return apiJson({ ...result, actor: current.actor });
  } catch (error) {
    return apiError(error);
  }
}

export async function relayReservationCancellation(request: Request, id: string) {
  try {
    requireSameOrigin(request);
    if (!isUuid(id)) {
      throw new HttpError(400, "Choose a valid reservation ID.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(25_000)]);
    const token = await sessionToken();
    if (!token) {
      throw new HttpError(401, "Sign in to continue.");
    }
    const current = await reservationActor(signal);
    if (current.denied) {
      return current.denied;
    }
    if (request.headers.get("content-type")?.split(";")[0] !== "application/json") {
      throw new HttpError(415, "Send application/json.");
    }
    const body = await readBoundedJson(request.body, signal, 8 * 1024);
    if (
      !isRecord(body) ||
      !isUuid(body.operationId) ||
      typeof body.reason !== "string" ||
      reservationCancellationReason(body.reason) !== body.reason
    ) {
      throw new HttpError(400, "Provide an operation ID and a valid cancellation reason.");
    }
    const result = await requestApiJson(
      `/api/reservations/${id}/cancellation`,
      parseReservationCancellation,
      {
        token,
        signal,
        method: "POST",
        body: { operationId: body.operationId, reason: body.reason },
      },
    );
    return apiJson({ ...result, actor: current.actor });
  } catch (error) {
    if (error instanceof SyntaxError) {
      return apiError(new HttpError(400, "Provide valid JSON details."));
    }
    return apiError(error);
  }
}
