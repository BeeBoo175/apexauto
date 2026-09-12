import "server-only";
import { API_DEADLINE_MS } from "@/lib/api";
import { isUuid } from "@/lib/auth/contracts";
import { HttpError } from "@/lib/http";
import { apiError, apiJson, requestApiJson } from "@/lib/server/http/api";
import { accessDenied, readJsonBody, relayGatedRead } from "@/lib/server/http/staff-relay";
import { administratorAccess, administratorSession } from "./account-administration-relay";
import {
  erasureConflict,
  erasurePath,
  erasurePreviewPath,
  parseErasurePreview,
  parseErasureRequest,
  type ErasureKind,
} from "./account-erasure";
import { parseClosureReceipt } from "./customer-closure";

const MAX_BODY_BYTES = 4 * 1024;

// The staff preview is the existing blockers relay; only the customer preview is new.
export function relayCustomerErasurePreview(request: Request, id: string) {
  if (!isUuid(id)) {
    return Promise.resolve(apiError(new HttpError(400, "Choose a valid account.")));
  }

  return relayGatedRead(request, {
    ...administratorAccess,
    path: erasurePreviewPath("customer", id),
    parse: (value) => (parseErasurePreview("customer", value) ? value : null),
  });
}

// A wrong password is a 409 with a code, never a 401, so a typo cannot sign the Administrator out. The password
// goes straight to the API and is not kept or logged here.
export async function relayErasure(request: Request, kind: ErasureKind, id: string) {
  try {
    if (!isUuid(id)) {
      throw new HttpError(400, "Choose a valid account.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const { token, actor, allowed } = await administratorSession(request, signal);
    if (!allowed) {
      return accessDenied(actor, administratorAccess.denial);
    }

    const body = parseErasureRequest(await readJsonBody(request, signal, MAX_BODY_BYTES));
    if (!body) {
      throw new HttpError(400, "Give a reason and enter your password to confirm.");
    }

    const result = await requestApiJson(
      erasurePath(kind, id),
      parseClosureReceipt,
      { token, signal, method: "POST", body },
      API_DEADLINE_MS,
      64 * 1024,
      erasureConflict,
    );
    return apiJson({ ...result, actor });
  } catch (error) {
    if (error instanceof SyntaxError) {
      return apiError(new HttpError(400, "Give a reason and enter your password to confirm."));
    }

    return apiError(error);
  }
}
