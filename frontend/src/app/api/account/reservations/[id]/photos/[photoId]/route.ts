import { isUuid } from "@/lib/auth/contracts";
import { HttpError, readBoundedBytes } from "@/lib/http";
import { apiError, sessionToken } from "@/lib/server/http/api";
import { requestApi } from "@/lib/server/http/upstream";
import { PHOTO_MAX_BYTES } from "@/lib/intake/contracts";
import { PHOTO_CACHE_CONTROL } from "@/lib/photo-cache";

export async function GET(
  request: Request,
  { params }: { params: Promise<{ id: string; photoId: string }> },
) {
  try {
    const { id, photoId } = await params;
    if (!isUuid(id) || !isUuid(photoId)) {
      throw new HttpError(400, "Choose a valid reservation and photo.");
    }
    const token = await sessionToken();
    if (!token) {
      throw new HttpError(401, "Sign in to view your reserved car.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(40_000)]);
    const { response } = await requestApi(
      `/api/account/reservations/${id}/photos/${photoId}`,
      40_000,
      { token, signal },
    );
    const contentType = response.headers.get("content-type") ?? "";
    if (!response.ok || !["image/jpeg", "image/png", "image/webp"].includes(contentType)) {
      await response.body?.cancel();
      throw new HttpError(
        response.ok ? 502 : response.status,
        "Reservation photography is unavailable.",
      );
    }
    const bytes = await readBoundedBytes(response.body, signal, PHOTO_MAX_BYTES);
    return new Response(Buffer.from(bytes), {
      headers: {
        "Content-Type": contentType,
        "Content-Length": String(bytes.byteLength),
        "Cache-Control": PHOTO_CACHE_CONTROL,
        "X-Content-Type-Options": "nosniff",
        "X-Robots-Tag": "noindex, nofollow",
      },
    });
  } catch (error) {
    return apiError(error);
  }
}
