import "server-only";
import { HttpError, readBoundedBytes, readBoundedJson } from "@/lib/http";
import { assistedCustomerId, requireSameOrigin } from "@/lib/server/http/request";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { requestApiJson, sessionToken, apiJson, apiError } from "@/lib/server/http/api";
import { API_DEADLINE_MS } from "@/lib/api";
import { PHOTO_DEADLINE_MS, PHOTO_MAX_BYTES, angles } from "./contracts";
import { confirmedActor } from "@/lib/server/http/staff-relay";
import { parseIntakeConflict } from "./errors";

const sessionUnconfirmed = "Your session could not be confirmed. Reload the page to try again.";

export async function relayIntake<T>(
  request: Request,
  path: string,
  parse: (value: unknown) => T | null,
  authenticated = false,
  maximumResponseBytes = 256 * 1024,
) {
  try {
    const method = request.method === "HEAD" ? "GET" : request.method;
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    if (method !== "GET") {
      requireSameOrigin(request);
    }
    const token = authenticated ? await sessionToken() : undefined;
    if (authenticated && !token) {
      throw new HttpError(401, "Sign in to continue.");
    }
    const actor = authenticated ? await confirmedActor(signal, sessionUnconfirmed) : null;
    let body: unknown;
    if (method !== "GET") {
      if (request.headers.get("content-type")?.split(";")[0] !== "application/json") {
        throw new HttpError(415, "Send application/json.");
      }
      try {
        body = await readBoundedJson(request.body, signal, 16 * 1024);
      } catch (error) {
        if (error instanceof SyntaxError) {
          throw new HttpError(400, "Provide a valid JSON object.");
        }
        throw error;
      }
      if (!isRecord(body)) {
        throw new HttpError(400, "Provide valid request details.");
      }
    }
    const result = await requestApiJson(
      path,
      parse,
      {
        token,
        body,
        method,
        signal,
        onBehalfOf: assistedCustomerId(request),
      },
      API_DEADLINE_MS,
      maximumResponseBytes,
      parseIntakeConflict,
    );
    return apiJson({ ...result, ...(actor ? { actor } : {}) });
  } catch (error) {
    return apiError(error);
  }
}

export async function relayPhoto(request: Request, id: string, parse: (value: unknown) => unknown) {
  try {
    requireSameOrigin(request);
    const token = await sessionToken();
    if (!token) {
      throw new HttpError(401, "Sign in to continue.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(PHOTO_DEADLINE_MS)]);
    const actor = await confirmedActor(signal, sessionUnconfirmed);
    const angle = new URL(request.url).searchParams.get("angle");
    const contentType = request.headers.get("content-type")?.split(";")[0];
    if (!isUuid(id) || !angles.some((item) => item === angle)) {
      throw new HttpError(400, "Choose a valid photo angle.");
    }
    if (!contentType || !["image/jpeg", "image/png", "image/webp"].includes(contentType)) {
      throw new HttpError(415, "Choose a JPEG, PNG or WebP photo.");
    }
    const bytes = await readBoundedBytes(request.body, signal, PHOTO_MAX_BYTES);
    const result = await requestApiJson(
      `/api/intake/photos/${id}?angle=${angle}`,
      parse,
      { token, bytes, contentType, method: "PUT", signal, onBehalfOf: assistedCustomerId(request) },
      PHOTO_DEADLINE_MS,
    );
    return apiJson({ ...result, actor });
  } catch (error) {
    return apiError(error);
  }
}

// Staff appointment routes validate the appointment reference once, then relay the named sub-resource.
export async function relayStaffAppointment<T>(
  request: Request,
  context: { params: Promise<{ id: string }> },
  resource: string,
  parse: (value: unknown) => T | null,
) {
  const { id } = await context.params;
  if (!isUuid(id)) {
    return apiError(new HttpError(400, "Provide a valid appointment reference."));
  }
  return relayIntake(request, `/api/staff/intake/appointments/${id}/${resource}`, parse, true);
}
