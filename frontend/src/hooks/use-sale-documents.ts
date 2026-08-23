"use client";

import { useEffect, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { privateApiFetch } from "@/lib/api-browser";
import { parseSaleDocuments, saleDocumentsPath, type SaleDocument } from "@/lib/sales/documents";

export function useSaleDocuments(saleId: string, version: number, active: boolean) {
  const [attempt, setAttempt] = useState(0);
  const [state, setState] = useState<{
    key: string;
    saleId: string;
    documents: SaleDocument[] | null;
    failed: boolean;
  } | null>(null);
  const { error } = useToast();
  const key = `${saleId}:${version}:${attempt}`;
  const settledKey = state?.key;

  useEffect(() => {
    if (!active || settledKey === key) {
      return;
    }
    const cancellation = new AbortController();
    const signal = AbortSignal.any([cancellation.signal, AbortSignal.timeout(22_000)]);
    async function load() {
      let denied = false;
      try {
        const response = await privateApiFetch(saleDocumentsPath(saleId), parseSaleDocuments, {
          signal,
        });
        if (cancellation.signal.aborted) {
          return;
        }
        denied = [401, 403, 404].includes(response.status);
        if (response.status !== 200 || !response.data) {
          throw new Error("Documents unavailable");
        }
        setState({ key, saleId, documents: response.data, failed: false });
      } catch {
        if (!cancellation.signal.aborted) {
          setState((current) => ({
            key,
            saleId,
            failed: true,
            documents: !denied && current?.saleId === saleId ? current.documents : null,
          }));
          error("Documents could not be loaded. Reload the page to try again.");
        }
      }
    }
    void load();
    return () => cancellation.abort();
  }, [saleId, key, active, settledKey, error]);

  return {
    documents: state?.saleId === saleId ? state.documents : null,
    failed: state?.key === key && state.failed,
    busy: active && settledKey !== key,
    retry: () => setAttempt((value) => value + 1),
  };
}
