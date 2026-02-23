import "server-only";
import { HttpError, readBoundedBytes, readBoundedJson } from "@/lib/http";
import { requireSameOrigin } from "@/lib/server/http/request";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { apiRead, sessionToken, apiJson, apiError } from "@/lib/server/http/api";
import { API_DEADLINE_MS } from "@/lib/api";
import { PHOTO_DEADLINE_MS, PHOTO_MAX_BYTES, angles } from "./contracts";
import { readCurrentActor } from "@/lib/auth/server";

export async function relayIntake<T>(
  request: Request,
  path: string,
  parse: (value: unknown) => T | null,
  authenticated = false,
  maximumResponseBytes = 256 * 1024,
) {
  try {
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    if (request.method !== "GET") {
      requireSameOrigin(request);
    }
    const token = authenticated ? await sessionToken() : undefined;
    if (authenticated && !token) {
      throw new HttpError(401, "Sign in to continue.");
    }
    const current = authenticated ? await readCurrentActor(signal) : null;
    const actor = current?.actor;
    if (current && current.status !== 200) {
      throw new HttpError(
        current.status === 401 ? 401 : 502,
        "Your session could not be confirmed. Retry when the service is available.",
      );
    }
    let body: unknown;
    if (request.method !== "GET") {
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
    const result = await apiRead(
      path,
      parse,
      {
        token,
        body,
        method: request.method,
        signal,
      },
      API_DEADLINE_MS,
      maximumResponseBytes,
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
    const current = await readCurrentActor(signal);
    const actor = current.actor;
    if (!actor) {
      throw new HttpError(
        current.status === 401 ? 401 : 502,
        "Your session could not be confirmed. Retry when the service is available.",
      );
    }
    const angle = new URL(request.url).searchParams.get("angle");
    const contentType = request.headers.get("content-type")?.split(";")[0];
    if (!isUuid(id) || !angles.some((item) => item === angle)) {
      throw new HttpError(400, "Choose a valid photo angle.");
    }
    if (!contentType || !["image/jpeg", "image/png", "image/webp"].includes(contentType)) {
      throw new HttpError(415, "Choose a JPEG, PNG or WebP photo.");
    }
    const bytes = await readBoundedBytes(request.body, signal, PHOTO_MAX_BYTES);
    const result = await apiRead(
      `/api/intake/photos/${id}?angle=${angle}`,
      parse,
      { token, bytes, contentType, method: "PUT", signal },
      PHOTO_DEADLINE_MS,
    );
    return apiJson({ ...result, actor });
  } catch (error) {
    return apiError(error);
  }
}
