import "server-only";
import { isUuid } from "@/lib/auth/contracts";
import { readCurrentActor } from "@/lib/auth/server";
import { requestApi } from "@/lib/server/http/upstream";
import { HttpError, readBoundedBytes } from "@/lib/http";
import { requireSameOrigin } from "@/lib/server/http/request";
import { intakeRead, intakeToken } from "@/lib/intake/server";
import { intakeError, intakeJson } from "@/lib/intake/routes";
import { PHOTO_MAX_BYTES } from "@/lib/intake/contracts";
import { parsePhoto } from "./parse-report";

export async function inspectionPhoto(request: Request, inspectionId: string, id: string) {
  try {
    if (!isUuid(inspectionId) || !isUuid(id)) {
      throw new HttpError(400, "Choose a valid inspection and photo ID.");
    }
    const upload = request.method === "PUT";
    const deadline = upload ? 90_000 : 40_000;
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(deadline)]);
    if (upload) {
      requireSameOrigin(request);
    }
    const token = await intakeToken();
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
    const path = `/api/staff/inspection/drafts/${inspectionId}/photos/${id}`;
    if (upload) {
      const source = new URL(request.url).searchParams;
      const pointCode = source.get("pointCode") ?? "";
      const version = Number(source.get("assignmentVersion"));
      const contentType = request.headers.get("content-type")?.split(";")[0] ?? "";
      if (!/^[A-Z]{2}-\d{3}$/.test(pointCode) || !Number.isSafeInteger(version) || version < 1) {
        throw new HttpError(400, "Choose a catalog point and current assignment version.");
      }
      if (!["image/jpeg", "image/png", "image/webp"].includes(contentType)) {
        throw new HttpError(415, "Choose a JPEG, PNG or WebP photo.");
      }
      const bytes = await readBoundedBytes(request.body, signal, PHOTO_MAX_BYTES);
      const query = new URLSearchParams({ pointCode, assignmentVersion: String(version) });
      const result = await intakeRead(
        `${path}?${query}`,
        parsePhoto,
        { token, signal, bytes, contentType, method: "PUT" },
        deadline,
      );
      return intakeJson({ ...result, actor: current.actor });
    }
    const { response, signal: bodySignal } = await requestApi(path, deadline, { token, signal });
    if (!response.ok) {
      await response.body?.cancel();
      throw new HttpError(
        response.status,
        "This private photo is unavailable. Refresh the inspection to check access.",
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
  } catch (error) {
    return intakeError(error);
  }
}
