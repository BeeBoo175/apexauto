"use client";

import { useEffect, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { privateApiFetch } from "@/lib/api-browser";
import { salePath } from "@/lib/sales/query";
import { parseSaleVehicle, type SaleVehicleReference } from "@/lib/sales/vehicle-reference";

export function useSaleVehicle(saleId: string, active: boolean) {
  const [attempt, setAttempt] = useState(0);
  const [state, setState] = useState<{
    key: string;
    saleId: string;
    result: SaleVehicleReference | null;
  } | null>(null);
  const { error } = useToast();
  const key = `${saleId}:${attempt}`;
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
        const response = await privateApiFetch(`${salePath(saleId)}/vehicle`, parseSaleVehicle, {
          signal,
        });
        if (cancellation.signal.aborted) {
          return;
        }
        if (response.status !== 200 || response.data?.saleId !== saleId) {
          denied = [401, 403, 404].includes(response.status);
          throw new Error("Vehicle reference unavailable");
        }
        setState({ key, saleId, result: response.data });
      } catch {
        if (!cancellation.signal.aborted) {
          setState((current) => ({
            key,
            saleId,
            result: !denied && current?.saleId === saleId ? current.result : null,
          }));
          error("The vehicle listing could not be loaded. Reload the page to try again.");
        }
      }
    }
    void load();
    return () => cancellation.abort();
  }, [saleId, key, error, active, settledKey]);

  return {
    result: state?.saleId === saleId ? state.result : null,
    busy: active && settledKey !== key,
    retry: () => setAttempt((current) => current + 1),
  };
}
