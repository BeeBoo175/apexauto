import "server-only";
import { isUuid } from "@/lib/auth/contracts";
import { HttpError, readBoundedBytes } from "@/lib/http";
import { PHOTO_MAX_BYTES, PHOTO_UPLOAD_MAX_BYTES } from "@/lib/intake/contracts";
import { apiError, apiJson, requestApiJson, sessionToken } from "@/lib/server/http/api";
import { requireSameOrigin } from "@/lib/server/http/request";
import { accessDenied, confirmedActor } from "@/lib/server/http/staff-relay";
import { requestApi } from "@/lib/server/http/upstream";
import { hasListingAccess } from "./listings";
import { parseListingPhotoReceipt } from "./listing-photos";

export async function listingPhoto(
  request: Request,
  vehicleId: string,
  photoId: string,
  mode: "download" | "upload" | "receipt",
) {
  try {
    if (!isUuid(vehicleId) || !isUuid(photoId)) {
      throw new HttpError(400, "Choose a valid vehicle and photo.");
    }
    const deadlines = { upload: 90_000, download: 40_000, receipt: 20_000 };
    const deadline = deadlines[mode];
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(deadline)]);
    if (mode === "upload") {
      requireSameOrigin(request);
    }
    const token = await sessionToken();
    if (!token) {
      throw new HttpError(401, "Sign in to continue.");
    }
    const actor = await confirmedActor(signal);
    if (!hasListingAccess(actor)) {
      return accessDenied(actor, "Listing access unavailable.");
    }
    const path = `/api/staff/listings/${vehicleId}/photos/${photoId}`;
    if (mode === "download") {
      return await downloadListingPhoto(path, token, signal);
    }
    if (mode === "receipt") {
      const result = await requestApiJson(
        `${path}/receipt`,
        parseListingPhotoReceipt,
        { token, signal },
        deadline,
      );
      return apiJson({ ...result, actor });
    }
    const contentType = request.headers.get("content-type")?.split(";")[0] ?? "";
    if (!["image/jpeg", "image/png", "image/webp"].includes(contentType)) {
      throw new HttpError(415, "Choose a JPEG, PNG or WebP photo.");
    }
    const bytes = await readBoundedBytes(request.body, signal, PHOTO_UPLOAD_MAX_BYTES);
    const result = await requestApiJson(
      path,
      parseListingPhotoReceipt,
      { token, signal, bytes, contentType, method: "PUT" },
      deadline,
    );
    return apiJson({ ...result, actor });
  } catch (error) {
    return apiError(error);
  }
}

export async function downloadListingPhoto(path: string, token: string, signal: AbortSignal) {
  const { response, signal: bodySignal } = await requestApi(path, 40_000, { token, signal });
  if (!response.ok) {
    await response.body?.cancel();
    throw new HttpError(
      response.status,
      "This listing photo is unavailable. Reload the page to try again.",
    );
  }
  const contentType = response.headers.get("content-type") ?? "";
  if (!["image/jpeg", "image/png", "image/webp"].includes(contentType)) {
    await response.body?.cancel();
    throw new HttpError(502, "Photo contents could not be confirmed.");
  }
  const bytes = await readBoundedBytes(response.body, bodySignal, PHOTO_MAX_BYTES);
  return new Response(Buffer.from(bytes), {
    headers: {
      "Content-Type": contentType,
      "Content-Length": String(bytes.byteLength),
      "Cache-Control": "private, no-store",
      "X-Content-Type-Options": "nosniff",
      "X-Robots-Tag": "noindex, nofollow",
    },
  });
}
