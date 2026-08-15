import "server-only";

import { isUuid } from "@/lib/auth/contracts";
import { API_DEADLINE_MS } from "@/lib/api";
import { hasIntakeAccess } from "@/lib/auth/actor";
import { HttpError } from "@/lib/http";
import { requireSameOrigin } from "@/lib/server/http/request";
import { apiError, apiJson, requestApiJson, sessionToken } from "@/lib/server/http/api";
import { confirmedActor } from "@/lib/server/http/staff-relay";
import {
  matchesDepositRequestCancellation,
  parseDepositRequestCancellation,
} from "./deposit-request-cancellation";

// Staff release a car they asked a deposit for. The backend authorizes the role and branch.
export async function relayDepositRequestCancellation(request: Request, requestId: string) {
  try {
    requireSameOrigin(request);
    if (!isUuid(requestId)) {
      throw new HttpError(400, "Choose a valid deposit request.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const token = await sessionToken();
    if (!token) {
      throw new HttpError(401, "Sign in to cancel a deposit request.");
    }
    const actor = await confirmedActor(signal);
    if (!hasIntakeAccess(actor)) {
      throw new HttpError(403, "Your role cannot cancel a deposit request.");
    }
    const result = await requestApiJson(
      `/api/reservation-requests/${requestId}/cancellations`,
      parseDepositRequestCancellation,
      { token, signal, method: "POST", body: {} },
      API_DEADLINE_MS,
    );
    if (result.data && !matchesDepositRequestCancellation(result.data, requestId)) {
      throw new HttpError(
        502,
        "The cancellation could not be confirmed. Reconcile the same request.",
      );
    }
    return apiJson({ ...result, actor });
  } catch (error) {
    return apiError(error);
  }
}
