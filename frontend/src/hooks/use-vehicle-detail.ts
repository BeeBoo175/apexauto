"use client";

import { useLayoutEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { apiFetch } from "@/lib/api-browser";
import type { ApiResult } from "@/lib/api";
import { parsePublicDetail, type PublicVehicleDetail } from "@/lib/vehicles/public-detail";
import { useInitialToast } from "./use-initial-toast";

export function useVehicleDetail(id: string, initial: ApiResult<PublicVehicleDetail>) {
  const [snapshot, setSnapshot] = useState(initial.data);
  const [busy, setBusy] = useState(false);
  const [unavailable, setUnavailable] = useState(initial.status === 404);
  const [entry, setEntry] = useState(initial);
  const controller = useRef<AbortController | null>(null);
  const { error } = useToast();
  useInitialToast(initial.status === 404 || initial.data ? null : initial.message);

  if (entry !== initial) {
    setEntry(initial);
    setBusy(false);
    if (initial.data || initial.status === 404) {
      setSnapshot(initial.data);
      setUnavailable(initial.status === 404);
    }
  }

  useLayoutEffect(() => {
    return () => {
      controller.current?.abort();
      controller.current = null;
    };
  }, [initial]);

  async function refresh() {
    if (controller.current) {
      return;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    try {
      const result = await apiFetch(
        `/api/vehicles/${id}`,
        (value) => parsePublicDetail(value, id),
        {
          signal: attempt.signal,
        },
      );
      if (attempt.signal.aborted || controller.current !== attempt) {
        return;
      }
      if (result.status === 404) {
        setSnapshot(null);
        setUnavailable(true);
        return;
      }
      if (!result.data) {
        throw new Error("Vehicle detail unavailable");
      }
      setSnapshot(result.data);
      setUnavailable(false);
    } catch {
      if (!attempt.signal.aborted && controller.current === attempt) {
        error(
          "The vehicle could not be refreshed. Your last confirmed details are still shown. Retry to check availability.",
        );
      }
    } finally {
      if (!attempt.signal.aborted && controller.current === attempt) {
        setBusy(false);
      }
      if (controller.current === attempt) {
        controller.current = null;
      }
    }
  }

  return { snapshot, unavailable, busy, refresh };
}
