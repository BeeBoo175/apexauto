import "server-only";
import { API_DEADLINE_MS } from "@/lib/api";
import { HttpError } from "@/lib/http";
import {
  apiError,
  apiJson,
  privateApiRead,
  requestApiJson,
  sessionToken,
} from "@/lib/server/http/api";
import { requireSameOrigin } from "@/lib/server/http/request";
import { readJsonBody, requireCustomerActor } from "@/lib/server/http/staff-relay";
import {
  CUSTOMER_CLOSURE_BLOCKERS_PATH,
  CUSTOMER_CLOSURE_PATH,
  closureConflict,
  parseClosurePreview,
  parseClosureReceipt,
  parseClosureRequest,
} from "./customer-closure";

const MAX_BODY_BYTES = 1024;

export async function relayClosureBlockers(request: Request) {
  try {
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const actor = await requireCustomerActor(signal);
    const result = await privateApiRead(
      CUSTOMER_CLOSURE_BLOCKERS_PATH,
      parseClosurePreview,
      signal,
    );
    return apiJson({ ...result, actor });
  } catch (error) {
    return apiError(error);
  }
}

// A wrong password is a 409 with a code, never a 401, so a typo cannot sign the browser out. The password goes
// straight to the API and is not kept or logged here.
export async function relayClosure(request: Request) {
  try {
    requireSameOrigin(request);
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const body = parseClosureRequest(await readJsonBody(request, signal, MAX_BODY_BYTES));
    if (!body) {
      throw new HttpError(400, "Enter your password to confirm.");
    }

    const actor = await requireCustomerActor(signal);
    const result = await requestApiJson(
      CUSTOMER_CLOSURE_PATH,
      parseClosureReceipt,
      { token: await sessionToken(), signal, method: "POST", body },
      API_DEADLINE_MS,
      64 * 1024,
      closureConflict,
    );
    return apiJson({ ...result, actor });
  } catch (error) {
    if (error instanceof SyntaxError) {
      return apiError(new HttpError(400, "Enter your password to confirm."));
    }

    return apiError(error);
  }
}
