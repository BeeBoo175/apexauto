import "server-only";
import { API_DEADLINE_MS } from "@/lib/api";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { HttpError, readBoundedJson } from "@/lib/http";
import { apiError, apiJson, requestApiJson, sessionToken } from "@/lib/server/http/api";
import { requireSameOrigin } from "@/lib/server/http/request";
import { accessDenied, confirmedActor, requireJsonRequest } from "@/lib/server/http/staff-relay";
import { relayAdministratorCommand } from "./account-admin-relay";
import {
  customerSupportActions,
  isCustomerSupportAction,
  parseNricReveal,
  shapeSupport,
} from "./customer-support";
import { hasAccountAdministration } from "./staff-accounts";

async function relayReveal(request: Request, path: string) {
  try {
    requireSameOrigin(request);
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const token = await sessionToken();
    if (!token) {
      throw new HttpError(401, "Sign in to continue.");
    }
    const actor = await confirmedActor(signal);
    if (!hasAccountAdministration(actor)) {
      return accessDenied(actor, "Administrator access required.");
    }
    requireJsonRequest(request);
    const body = await readBoundedJson(request.body, signal, 4 * 1024);
    const forwarded = isRecord(body) ? shapeSupport(body) : null;
    if (!forwarded) {
      throw new HttpError(400, "Provide a reason of at most 1000 characters.");
    }
    const result = await requestApiJson(path, parseNricReveal, {
      token,
      signal,
      method: "POST",
      body: forwarded,
    });
    return apiJson({ ...result, actor });
  } catch (error) {
    return apiError(error);
  }
}

export function relayCustomerSupport(request: Request, id: string, action: string) {
  if (!isUuid(id) || !isCustomerSupportAction(action)) {
    return Promise.resolve(apiError(new HttpError(400, "Choose a valid customer and action.")));
  }
  const path = `/api/customer-accounts/${id}/${customerSupportActions[action].upstream}`;
  if (action === "nric-reveal") {
    return relayReveal(request, path);
  }
  return relayAdministratorCommand(request, {
    path,
    method: "POST",
    shape: shapeSupport,
    conflict: () =>
      "This action is not available now: the account may be suspended, already verified, or the email cannot be sent yet.",
  });
}
