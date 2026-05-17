import "server-only";
import { isUuid } from "@/lib/auth/contracts";
import { requestApi } from "@/lib/server/http/upstream";
import { HttpError, readBoundedBytes } from "@/lib/http";
import { confirmedActor } from "@/lib/server/http/staff-relay";
import { apiError, sessionToken } from "@/lib/server/http/api";

const REPORT_DOCUMENT_DEADLINE_MS = 65_000;
const REPORT_DOCUMENT_MAX_BYTES = 64 * 1024 * 1024;

export async function inspectionReportDocument(
  request: Request,
  inspectionId: string,
  revision: string,
) {
  try {
    if (!isUuid(inspectionId)) {
      throw new HttpError(400, "Choose a valid inspection ID.");
    }
    if (
      !/^\d+$/.test(revision) ||
      !Number.isSafeInteger(Number(revision)) ||
      Number(revision) < 1
    ) {
      throw new HttpError(400, "Choose a valid report revision.");
    }
    const signal = AbortSignal.any([
      request.signal,
      AbortSignal.timeout(REPORT_DOCUMENT_DEADLINE_MS),
    ]);
    const token = await sessionToken();
    if (!token) {
      throw new HttpError(401, "Sign in to continue.");
    }
    await confirmedActor(signal);
    const { response, signal: bodySignal } = await requestApi(
      `/api/staff/inspection/reports/${inspectionId}/revisions/${revision}/document`,
      REPORT_DOCUMENT_DEADLINE_MS,
      { token, signal },
    );
    if (!response.ok) {
      await response.body?.cancel();
      throw new HttpError(
        response.status,
        "This signed report is unavailable. Refresh the inspection to check access.",
      );
    }
    if (response.headers.get("content-type") !== "application/pdf") {
      await response.body?.cancel();
      throw new HttpError(502, "Report contents could not be confirmed.");
    }
    const bytes = await readBoundedBytes(response.body, bodySignal, REPORT_DOCUMENT_MAX_BYTES);
    return new Response(Buffer.from(bytes), {
      headers: {
        "Content-Type": "application/pdf",
        "Content-Length": String(bytes.byteLength),
        "Content-Disposition": `inline; filename="inspection-report-${inspectionId}-revision-${revision}.pdf"`,
        "Cache-Control": "private, no-store",
        "X-Content-Type-Options": "nosniff",
        "X-Robots-Tag": "noindex, nofollow",
      },
    });
  } catch (error) {
    return apiError(error);
  }
}
