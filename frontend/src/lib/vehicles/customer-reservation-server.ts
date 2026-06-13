import "server-only";

import { readCurrentActor, readOwnAccount } from "@/lib/auth/server";
import { isUuid } from "@/lib/auth/contracts";
import { API_DEADLINE_MS } from "@/lib/api";
import { HttpError, readBoundedJson } from "@/lib/http";
import { requireSameOrigin } from "@/lib/server/http/request";
import { apiError, apiJson, requestApiJson, sessionToken } from "@/lib/server/http/api";
import {
  parseCustomerReservation,
  parseCustomerReservationRequest,
  type ReservationAccess,
} from "./customer-reservation";
import { parseReservationConflict } from "./reservation-errors";

export async function readReservationAccess(signal?: AbortSignal): Promise<ReservationAccess> {
  try {
    const current = await readCurrentActor(signal);
    if (!current.actor || current.actor.role !== "Customer") {
      return { status: current.actor ? 403 : current.status, actor: current.actor, account: null };
    }
    const own = await readOwnAccount(signal);
    if (own.account?.userId !== current.actor.userId) {
      return { status: 502, actor: current.actor, account: null };
    }
    return { status: own.status, actor: current.actor, account: own.account };
  } catch {
    return { status: 502, actor: null, account: null };
  }
}

export async function relayCustomerReservation(request: Request, vehicleId: string) {
  try {
    requireSameOrigin(request);
    if (!isUuid(vehicleId)) {
      throw new HttpError(400, "Choose a valid vehicle.");
    }
    if (
      request.headers.get("content-type")?.split(";")[0].trim().toLowerCase() !== "application/json"
    ) {
      throw new HttpError(415, "Send application/json.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const body = parseCustomerReservationRequest(
      await readBoundedJson(request.body, signal, 8 * 1024),
    );
    if (!body) {
      throw new HttpError(400, "Provide valid reservation details and an operation ID.");
    }
    const token = await sessionToken();
    if (!token) {
      throw new HttpError(401, "Sign in to reserve this vehicle.");
    }
    const access = await readReservationAccess(signal);
    if (access.status !== 200 || !access.actor || !access.account) {
      throw new HttpError(access.status, "Your customer account could not be confirmed.");
    }
    if (!access.account.emailVerified) {
      throw new HttpError(403, "Verify your email before reserving a vehicle.");
    }
    const result = await requestApiJson(
      `/api/vehicles/${vehicleId}/reservations`,
      parseCustomerReservation,
      { token, signal, method: "POST", body },
      API_DEADLINE_MS,
      undefined,
      parseReservationConflict,
    );
    // Even a readable success must belong to this exact customer's captured operation.
    if (
      result.data &&
      (result.data.id !== body.operationId ||
        result.data.vehicleId !== vehicleId ||
        result.data.publicationId !== body.publicationId ||
        result.data.promoId !== body.promoId ||
        result.data.buyer.email !== access.account.email)
    ) {
      throw new HttpError(
        502,
        "The reservation receipt could not be confirmed. Reconcile the same request.",
      );
    }
    return apiJson({ ...result, actor: access.actor });
  } catch (error) {
    if (error instanceof SyntaxError) {
      return apiError(new HttpError(400, "Provide valid JSON details."));
    }
    return apiError(error);
  }
}
