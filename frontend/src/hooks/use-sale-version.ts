"use client";

import { useCallback, useEffect, useState } from "react";
import { privateApiFetch } from "@/lib/api-browser";
import { parseSaleVersion, saleVersionPath, type SaleVersion } from "@/lib/sales/sale-history";

// One past version of a sale, read when it is opened and again only on an explicit retry.
export function useSaleVersion(saleId: string, version: number | null) {
  const [state, setState] = useState<{
    key: string;
    resource: string;
    result: SaleVersion | null;
    failed: boolean;
  }>({
    key: "",
    resource: "",
    result: null,
    failed: false,
  });
  const [attempt, setAttempt] = useState(0);
  const resource = `${saleId}:${version}`;
  const key = `${resource}:${attempt}`;

  useEffect(() => {
    if (version === null) {
      return;
    }
    const cancellation = new AbortController();
    const signal = AbortSignal.any([cancellation.signal, AbortSignal.timeout(22_000)]);
    privateApiFetch(saleVersionPath(saleId, version), parseSaleVersion, { signal })
      .then((response) => {
        if (cancellation.signal.aborted) {
          return;
        }
        const matches =
          response.status === 200 &&
          response.data?.saleId === saleId &&
          response.data.version === version;
        setState((current) => {
          const retained = current.resource === resource ? current.result : null;
          return {
            key,
            resource,
            result: matches ? response.data : retained,
            failed: !matches,
          };
        });
      })
      .catch(() => {
        if (!cancellation.signal.aborted) {
          setState((current) => ({
            key,
            resource,
            result: current.resource === resource ? current.result : null,
            failed: true,
          }));
        }
      });
    return () => cancellation.abort();
  }, [saleId, version, key, resource]);

  const current = state.key === key ? state : null;
  const retry = useCallback(() => setAttempt((value) => value + 1), []);
  return {
    result: version !== null && state.resource === resource ? state.result : null,
    busy: version !== null && current === null,
    failed: version !== null && (current?.failed ?? false),
    retry,
  };
}
