import "server-only";

import { isUuid } from "@/lib/auth/contracts";
import { API_DEADLINE_MS } from "@/lib/api";
import { hasIntakeAccess } from "@/lib/auth/actor";
import { HttpError, readBoundedJson } from "@/lib/http";
import { assistedCustomerId, requireSameOrigin } from "@/lib/server/http/request";
import { apiError, apiJson, requestApiJson, sessionToken } from "@/lib/server/http/api";
import { confirmedActor } from "@/lib/server/http/staff-relay";
import { parseCustomerReservationRequest } from "./customer-reservation";
import { matchesDepositRequest, parseDepositRequestReceipt } from "./deposit-request";
import { parseReservationConflict } from "./reservation-errors";

// Only staff ask for a deposit, for the customer they name; a customer reserves and pays for themselves.
export async function relayDepositRequest(request: Request, vehicleId: string) {
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
      throw new HttpError(401, "Sign in to ask a customer for a deposit.");
    }
    const onBehalfOf = assistedCustomerId(request);
    if (!onBehalfOf) {
      throw new HttpError(400, "Choose the customer to ask for a deposit.");
    }
    const actor = await confirmedActor(signal);
    if (!hasIntakeAccess(actor)) {
      throw new HttpError(403, "Your role cannot reserve for a customer.");
    }
    const result = await requestApiJson(
      `/api/vehicles/${vehicleId}/reservation-requests`,
      parseDepositRequestReceipt,
      { token, signal, method: "POST", body, onBehalfOf },
      API_DEADLINE_MS,
      undefined,
      (code) => parseReservationConflict(code, true),
    );
    if (result.data && !matchesDepositRequest(result.data, body, vehicleId)) {
      throw new HttpError(
        502,
        "The deposit request could not be confirmed. Reconcile the same request.",
      );
    }
    return apiJson({ ...result, actor });
  } catch (error) {
    if (error instanceof SyntaxError) {
      return apiError(new HttpError(400, "Provide valid JSON details."));
    }
    return apiError(error);
  }
}
