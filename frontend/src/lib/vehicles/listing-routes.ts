import "server-only";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { readCurrentActor } from "@/lib/auth/server";
import { HttpError, readBoundedJson } from "@/lib/http";
import { requireSameOrigin } from "@/lib/server/http/request";
import {
  apiError,
  apiJson,
  requestApiJson,
  privateApiRead,
  sessionToken,
} from "@/lib/server/http/api";
import { hasListingAccess } from "@/lib/vehicles/listings";

export async function relayListingRead<T>(
  request: Request,
  id: string,
  resource: string,
  parse: (value: unknown) => T | null,
  matches: (value: T) => boolean,
) {
  try {
    if (!isUuid(id)) {
      throw new HttpError(400, "Choose a valid vehicle ID.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(25_000)]);
    const current = await readCurrentActor(signal);
    if (!current.actor) {
      throw new HttpError(
        current.status === 401 ? 401 : 502,
        "Your session could not be confirmed.",
      );
    }
    if (!hasListingAccess(current.actor)) {
      return apiJson({
        status: 403,
        data: null,
        message: "Listing access unavailable.",
        uncertain: false,
        actor: current.actor,
      });
    }
    const result = await privateApiRead(`/api/staff/listings/${id}${resource}`, parse, signal);
    if (result.data && !matches(result.data)) {
      throw new HttpError(502, "The listing response could not be confirmed.");
    }
    return apiJson({ ...result, actor: current.actor });
  } catch (error) {
    return apiError(error);
  }
}

export async function relayListingCommand<T>(
  request: Request,
  id: string,
  action: "drafts" | "publications" | "withdrawals",
  parse: (value: unknown) => T | null,
) {
  try {
    requireSameOrigin(request);
    if (!isUuid(id)) {
      throw new HttpError(400, "Choose a valid vehicle ID.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(25_000)]);
    const token = await sessionToken();
    if (!token) {
      throw new HttpError(401, "Sign in to continue.");
    }
    const current = await readCurrentActor(signal);
    if (!current.actor) {
      throw new HttpError(
        current.status === 401 ? 401 : 502,
        "Your session could not be confirmed.",
      );
    }
    if (!hasListingAccess(current.actor)) {
      return apiJson({
        status: 403,
        data: null,
        message: "Listing access unavailable.",
        uncertain: false,
        actor: current.actor,
      });
    }
    if (request.headers.get("content-type")?.split(";")[0] !== "application/json") {
      throw new HttpError(415, "Send application/json.");
    }
    const body = await readBoundedJson(request.body, signal, 32 * 1024);
    if (!isRecord(body) || !isUuid(body.operationId)) {
      throw new HttpError(400, "Provide an operation ID and valid listing details.");
    }
    const result = await requestApiJson(`/api/staff/listings/${id}/${action}`, parse, {
      token,
      signal,
      method: "POST",
      body,
    });
    return apiJson({ ...result, actor: current.actor });
  } catch (error) {
    if (error instanceof SyntaxError) {
      return apiError(new HttpError(400, "Provide valid JSON details."));
    }
    return apiError(error);
  }
}
