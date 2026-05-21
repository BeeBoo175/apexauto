"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { privateApiFetch } from "@/lib/api-browser";
import { parseSaleDocument, saleDocumentsPath, type SaleDocument } from "@/lib/sales/documents";

export function useSaleDocumentVersion(saleId: string, initial: SaleDocument) {
  const [document, setDocument] = useState<SaleDocument | null>(initial);
  const [busy, setBusy] = useState(false);
  const cancellation = useRef<AbortController | null>(null);
  const { error } = useToast();
  useEffect(() => () => cancellation.current?.abort(), []);

  async function open(documentId: string) {
    if (cancellation.current) {
      return;
    }
    const attempt = new AbortController();
    cancellation.current = attempt;
    setBusy(true);
    let denied = false;
    try {
      const response = await privateApiFetch(
        `${saleDocumentsPath(saleId)}/${documentId}`,
        parseSaleDocument,
        {
          signal: AbortSignal.any([attempt.signal, AbortSignal.timeout(22_000)]),
        },
      );
      if (attempt.signal.aborted) {
        return;
      }
      denied = [401, 403, 404].includes(response.status);
      if (
        response.status !== 200 ||
        response.data?.id !== documentId ||
        response.data.kind !== initial.kind ||
        response.data.contents.customerId !== initial.contents.customerId
      ) {
        throw new Error("Document version unavailable");
      }
      setDocument(response.data);
    } catch {
      if (!attempt.signal.aborted) {
        if (denied) {
          setDocument(null);
        }
        error(
          "The document version could not be loaded. Retry deliberately to check access and load it again.",
        );
      }
    } finally {
      cancellation.current = null;
      if (!attempt.signal.aborted) {
        setBusy(false);
      }
    }
  }
  return { document, busy, open };
}
