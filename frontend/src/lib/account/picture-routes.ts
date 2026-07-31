import "server-only";
import { isUuid } from "@/lib/auth/contracts";
import { HttpError, parseRetryAfterSeconds, readBoundedBytes } from "@/lib/http";
import { PHOTO_DEADLINE_MS, PHOTO_MAX_BYTES } from "@/lib/intake/contracts";
import { apiError, apiJson, requestApiJson, sessionToken } from "@/lib/server/http/api";
import { requireSameOrigin } from "@/lib/server/http/request";
import { confirmedActor } from "@/lib/server/http/staff-relay";
import { requestApi } from "@/lib/server/http/upstream";
import { PICTURE_CONTENT_TYPES, parsePictureReceipt } from "./contracts";

const PICTURE_READ_DEADLINE_MS = 40_000;

async function requireToken() {
  const token = await sessionToken();
  if (!token) {
    throw new HttpError(401, "Sign in to continue.");
  }
  return token;
}

export async function uploadOwnPicture(request: Request) {
  try {
    requireSameOrigin(request);
    const token = await requireToken();
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(PHOTO_DEADLINE_MS)]);
    const actor = await confirmedActor(signal);
    const contentType = request.headers.get("content-type")?.split(";")[0] ?? "";
    if (!PICTURE_CONTENT_TYPES.includes(contentType)) {
      throw new HttpError(415, "Choose a JPEG, PNG or WebP picture.");
    }
    const bytes = await readBoundedBytes(request.body, signal, PHOTO_MAX_BYTES);
    const result = await requestApiJson(
      "/api/accounts/me/picture",
      parsePictureReceipt,
      { token, signal, bytes, contentType, method: "PUT" },
      PHOTO_DEADLINE_MS,
    );
    return apiJson({ ...result, actor });
  } catch (error) {
    return apiError(error);
  }
}

// The backend answers 204 with no body; the relay reports it in the shared envelope.
export async function removePicture(request: Request, accountId: string) {
  try {
    requireSameOrigin(request);
    if (!isUuid(accountId)) {
      throw new HttpError(400, "Choose a valid account.");
    }
    const token = await requireToken();
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(PICTURE_READ_DEADLINE_MS)]);
    const actor = await confirmedActor(signal);
    const { response } = await requestApi(
      `/api/accounts/${accountId}/picture`,
      PICTURE_READ_DEADLINE_MS,
      { token, signal, method: "DELETE" },
    );
    await response.body?.cancel();
    if (response.status >= 500) {
      throw new Error("Unconfirmed picture removal");
    }
    const removed = response.status === 204;
    return apiJson({
      status: removed ? 200 : response.status,
      data: removed ? { removed: true } : null,
      message: removed ? "" : "This picture cannot be removed by your account.",
      uncertain: false,
      actor,
    });
  } catch (error) {
    return apiError(error);
  }
}

export async function downloadPicture(request: Request, accountId: string) {
  try {
    if (!isUuid(accountId)) {
      throw new HttpError(400, "Choose a valid account.");
    }
    const token = await requireToken();
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(PICTURE_READ_DEADLINE_MS)]);
    const { response, signal: bodySignal } = await requestApi(
      `/api/accounts/${accountId}/picture`,
      PICTURE_READ_DEADLINE_MS,
      { token, signal },
    );
    if (!response.ok) {
      await response.body?.cancel();
      throw new HttpError(
        response.status,
        "This picture is unavailable.",
        response.status === 429
          ? parseRetryAfterSeconds(response.headers.get("retry-after"))
          : undefined,
      );
    }
    const contentType = response.headers.get("content-type") ?? "";
    if (!PICTURE_CONTENT_TYPES.includes(contentType)) {
      await response.body?.cancel();
      throw new HttpError(502, "Picture contents could not be confirmed.");
    }
    const bytes = await readBoundedBytes(response.body, bodySignal, PHOTO_MAX_BYTES);
    return new Response(Buffer.from(bytes), {
      headers: {
        "Content-Type": contentType,
        "Content-Length": String(bytes.byteLength),
        // The version in the URL changes with the picture; keying on the session cookie keeps one
        // account's cached picture from being served to another account in the same browser.
        "Cache-Control": "private, max-age=600",
        Vary: "Cookie",
        "X-Content-Type-Options": "nosniff",
        "X-Robots-Tag": "noindex, nofollow",
      },
    });
  } catch (error) {
    return apiError(error);
  }
}
