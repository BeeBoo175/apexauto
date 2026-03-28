import { isUuid } from "@/lib/auth/contracts";
import { readCurrentActor } from "@/lib/auth/server";
import { HttpError, readBoundedBytes } from "@/lib/http";
import { PHOTO_MAX_BYTES } from "@/lib/intake/contracts";
import { apiError, sessionToken } from "@/lib/server/http/api";
import { requestApi } from "@/lib/server/http/upstream";
import { hasListingAccess } from "@/lib/vehicles/listings";

export async function GET(
  request: Request,
  { params }: { params: Promise<{ id: string; photoId: string }> },
) {
  try {
    const { id, photoId } = await params;
    if (!isUuid(id) || !isUuid(photoId)) {
      throw new HttpError(400, "Choose a valid vehicle and photo.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(40_000)]);
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
      throw new HttpError(403, "Listing access unavailable.");
    }
    const { response, signal: bodySignal } = await requestApi(
      `/api/staff/listings/${id}/photos/${photoId}`,
      40_000,
      { token, signal },
    );
    if (!response.ok) {
      await response.body?.cancel();
      throw new HttpError(response.status, "This listing photo is unavailable. Retry the photo.");
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
  } catch (error) {
    return apiError(error);
  }
}
