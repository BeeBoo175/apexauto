import { setTimeout as delay } from "node:timers/promises";
import { isUuid } from "@/lib/auth/contracts";
import { HttpError, parseRetryAfterSeconds, readBoundedBytes } from "@/lib/http";
import { apiError } from "@/lib/server/http/api";
import { requestApi } from "@/lib/server/http/upstream";
import { PHOTO_MAX_BYTES } from "@/lib/intake/contracts";

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
    const path = `/api/vehicles/${id}/photos/${photoId}`;
    let result = await requestApi(path, 40_000, { signal });
    const retryAfter = parseRetryAfterSeconds(result.response.headers.get("retry-after"));
    if (result.response.status === 429 && retryAfter !== undefined && retryAfter <= 2) {
      await result.response.body?.cancel();
      // One bounded read retry handles the shared photo-transfer concurrency limit.
      await delay(retryAfter * 1000, undefined, { signal });
      result = await requestApi(path, 40_000, { signal });
    }
    const { response } = result;
    if (!response.ok) {
      await response.body?.cancel();
      throw new HttpError(
        response.status,
        "This vehicle photo is unavailable. Reload the page to try again.",
      );
    }
    const contentType = response.headers.get("content-type") ?? "";
    if (!["image/jpeg", "image/png", "image/webp"].includes(contentType)) {
      await response.body?.cancel();
      throw new HttpError(502, "Photo contents could not be confirmed.");
    }
    const bytes = await readBoundedBytes(response.body, signal, PHOTO_MAX_BYTES);
    return new Response(Buffer.from(bytes), {
      headers: {
        "Content-Type": contentType,
        "Content-Length": String(bytes.byteLength),
        "Cache-Control": "no-store",
        "X-Content-Type-Options": "nosniff",
      },
    });
  } catch (error) {
    return apiError(error);
  }
}
