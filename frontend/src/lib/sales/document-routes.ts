import "server-only";
import { isUuid } from "@/lib/auth/contracts";
import { HttpError, readBoundedBytes } from "@/lib/http";
import { apiError, apiJson, privateApiRead, sessionToken } from "@/lib/server/http/api";
import { confirmedActor } from "@/lib/server/http/staff-relay";
import { requestApi } from "@/lib/server/http/upstream";
import { parseSaleDocument, parseSaleDocuments } from "./documents";

const DOCUMENT_DEADLINE_MS = 25_000;
const DOCUMENT_MAX_BYTES = 8 * 1024 * 1024;

export async function relaySaleDocuments(request: Request, saleId: string, documentId?: string) {
  try {
    validateIds(saleId, documentId);
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(DOCUMENT_DEADLINE_MS)]);
    const actor = await confirmedActor(signal);
    const path = `/api/sales/${saleId}/documents`;
    if (documentId) {
      const result = await privateApiRead(
        `${path}/${documentId}`,
        (value) => {
          const document = parseSaleDocument(value);
          return document?.id === documentId ? document : null;
        },
        signal,
      );
      return apiJson({ ...result, actor });
    }
    const result = await privateApiRead(path, parseSaleDocuments, signal);
    return apiJson({ ...result, actor });
  } catch (error) {
    return apiError(error);
  }
}

export async function relaySaleDocumentPdf(request: Request, saleId: string, documentId: string) {
  try {
    validateIds(saleId, documentId);
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(DOCUMENT_DEADLINE_MS)]);
    const token = await sessionToken();
    if (!token) {
      throw new HttpError(401, "Sign in to continue.");
    }
    await confirmedActor(signal);
    const { response, signal: bodySignal } = await requestApi(
      `/api/sales/${saleId}/documents/${documentId}/pdf`,
      DOCUMENT_DEADLINE_MS,
      { token, signal },
    );
    if (!response.ok) {
      await response.body?.cancel();
      throw new HttpError(
        response.status,
        "This document is unavailable. Refresh the sale to check access.",
      );
    }
    if (response.headers.get("content-type") !== "application/pdf") {
      await response.body?.cancel();
      throw new HttpError(502, "Document contents could not be confirmed.");
    }
    const bytes = await readBoundedBytes(response.body, bodySignal, DOCUMENT_MAX_BYTES);
    return new Response(Buffer.from(bytes), {
      headers: {
        "Content-Type": "application/pdf",
        "Content-Length": String(bytes.byteLength),
        "Content-Disposition": `inline; filename="sale-document-${documentId}.pdf"`,
        "Cache-Control": "private, no-store",
        "X-Content-Type-Options": "nosniff",
        "X-Robots-Tag": "noindex, nofollow",
      },
    });
  } catch (error) {
    return apiError(error);
  }
}

function validateIds(saleId: string, documentId?: string) {
  if (!isUuid(saleId) || (documentId !== undefined && !isUuid(documentId))) {
    throw new HttpError(400, "Choose a valid sale and document.");
  }
}
