import "server-only";
import { isUuid } from "@/lib/auth/contracts";
import { HttpError, parseRetryAfterSeconds, readBoundedBytes } from "@/lib/http";
import { PHOTO_CACHE_CONTROL } from "@/lib/photo-cache";
import { PHOTO_DEADLINE_MS, PHOTO_MAX_BYTES, PHOTO_UPLOAD_MAX_BYTES } from "@/lib/intake/contracts";
import { apiError, apiJson, requestApiJson, sessionToken } from "@/lib/server/http/api";
import { requireSameOrigin } from "@/lib/server/http/request";
import { confirmedActor } from "@/lib/server/http/staff-relay";
import { requestApi } from "@/lib/server/http/upstream";
import { PICTURE_CONTENT_TYPES, parsePictureReceipt, parseRemovedPicture } from "./contracts";

const PICTURE_READ_DEADLINE_MS = 40_000;
const PICTURE_RECEIPT_DEADLINE_MS = 20_000;

async function requireToken() {
  const token = await sessionToken();
  if (!token) {
    throw new HttpError(401, "Sign in to continue.");
  }
  return token;
}

// The browser chooses the picture id once per picked file and reuses it on every retry of that upload.
export async function uploadOwnPicture(request: Request, pictureId: string) {
  try {
    requireSameOrigin(request);
    if (!isUuid(pictureId)) {
      throw new HttpError(400, "Choose a valid picture.");
    }
    const token = await requireToken();
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(PHOTO_DEADLINE_MS)]);
    const actor = await confirmedActor(signal);
    const contentType = request.headers.get("content-type")?.split(";")[0] ?? "";
    if (!PICTURE_CONTENT_TYPES.includes(contentType)) {
      throw new HttpError(415, "Choose a JPEG, PNG or WebP picture.");
    }
    const bytes = await readBoundedBytes(request.body, signal, PHOTO_UPLOAD_MAX_BYTES);
    const result = await requestApiJson(
      `/api/accounts/me/picture/${pictureId}`,
      parsePictureReceipt,
      { token, signal, bytes, contentType, method: "PUT" },
      PHOTO_DEADLINE_MS,
    );
    return apiJson({ ...result, actor });
  } catch (error) {
    return apiError(error);
  }
}

export async function ownPictureReceipt(request: Request, pictureId: string) {
  try {
    if (!isUuid(pictureId)) {
      throw new HttpError(400, "Choose a valid picture.");
    }
    const token = await requireToken();
    const signal = AbortSignal.any([
      request.signal,
      AbortSignal.timeout(PICTURE_RECEIPT_DEADLINE_MS),
    ]);
    const actor = await confirmedActor(signal);
    const result = await requestApiJson(
      `/api/accounts/me/picture/${pictureId}/receipt`,
      parsePictureReceipt,
      { token, signal },
      PICTURE_RECEIPT_DEADLINE_MS,
    );
    return apiJson({ ...result, actor });
  } catch (error) {
    return apiError(error);
  }
}

// Removal names the picture that was seen; the backend never resolves it to the account's current picture.
export async function removePicture(request: Request, accountId: string, pictureId: string) {
  try {
    requireSameOrigin(request);
    if (!isUuid(accountId) || !isUuid(pictureId)) {
      throw new HttpError(400, "Choose a valid account and picture.");
    }
    const token = await requireToken();
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(PICTURE_READ_DEADLINE_MS)]);
    const actor = await confirmedActor(signal);
    const result = await requestApiJson(
      `/api/accounts/${accountId}/picture/${pictureId}`,
      parseRemovedPicture,
      { token, signal, method: "DELETE" },
      PICTURE_READ_DEADLINE_MS,
    );
    return apiJson({ ...result, actor });
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
        // Only an address that names the picture version may be kept: without it, a replaced picture would
        // keep showing under the same address.
        "Cache-Control": isUuid(new URL(request.url).searchParams.get("v") ?? "")
          ? PHOTO_CACHE_CONTROL
          : "no-store",
        "X-Content-Type-Options": "nosniff",
        "X-Robots-Tag": "noindex, nofollow",
      },
    });
  } catch (error) {
    return apiError(error);
  }
}
