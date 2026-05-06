"use client";

import { useCallback, useEffect, useState } from "react";
import { privateApiFetch } from "@/lib/api-browser";
import { parseSaleVersion, saleVersionPath, type SaleVersion } from "@/lib/sales/sale-history";

// One past version of a sale, read when it is opened and again only on an explicit retry.
export function useSaleVersion(saleId: string, version: number) {
  const [state, setState] = useState<{ key: string; result: SaleVersion | null; failed: boolean }>({
    key: "",
    result: null,
    failed: false,
  });
  const [attempt, setAttempt] = useState(0);
  const key = `${saleId}:${version}:${attempt}`;

  useEffect(() => {
    const cancellation = new AbortController();
    const signal = AbortSignal.any([cancellation.signal, AbortSignal.timeout(22_000)]);
    privateApiFetch(saleVersionPath(saleId, version), parseSaleVersion, { signal })
      .then((response) => {
        const matches =
          response.status === 200 &&
          response.data?.saleId === saleId &&
          response.data.version === version;
        setState({ key, result: matches ? response.data : null, failed: !matches });
      })
      .catch(() => {
        if (!cancellation.signal.aborted) {
          setState({ key, result: null, failed: true });
        }
      });
    return () => cancellation.abort();
  }, [saleId, version, key]);

  const current = state.key === key ? state : null;
  const retry = useCallback(() => setAttempt((value) => value + 1), []);
  return {
    result: current?.result ?? null,
    busy: current === null,
    failed: current?.failed ?? false,
    retry,
  };
}
