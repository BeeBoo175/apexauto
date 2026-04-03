"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import type { AppointmentWorkbench } from "@/components/staff/AppointmentWorkbench";
import { useToast } from "@/components/ui/Toast";
import { apiFetch } from "@/lib/api-browser";
import type { ApiResult } from "@/lib/api";
import type { AppointmentDetail } from "@/lib/staff/contracts";
import { parseDetail } from "@/lib/staff/parse";

interface AppointmentEntry {
  Workbench: typeof AppointmentWorkbench;
  initial: ApiResult<AppointmentDetail>;
}

export function useAppointmentClientEntry(id: string) {
  const [entry, setEntry] = useState<AppointmentEntry | null>(null);
  const [failed, setFailed] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const { error } = useToast();

  const load = useCallback(async () => {
    if (controller.current) {
      return;
    }
    const attempt = new AbortController();
    const signal = AbortSignal.any([attempt.signal, AbortSignal.timeout(22_000)]);
    controller.current = attempt;
    setFailed(false);
    try {
      const [initial, { AppointmentWorkbench: Workbench }] = await Promise.all([
        apiFetch(
          `/api/staff/intake/appointments/${id}`,
          parseDetail,
          { signal },
          { requireActor: true },
        ),
        import("@/components/staff/AppointmentWorkbench"),
      ]);
      if (attempt.signal.aborted || controller.current !== attempt) {
        return;
      }
      signal.throwIfAborted();
      if (initial.status !== 200 || initial.data?.id !== id) {
        throw new Error("Appointment unavailable");
      }
      setEntry({ Workbench, initial });
    } catch {
      if (!attempt.signal.aborted && controller.current === attempt) {
        setFailed(true);
        error("The appointment could not be loaded. Retry to open it.");
      }
    } finally {
      attempt.abort();
      if (controller.current === attempt) {
        controller.current = null;
      }
    }
  }, [id, error]);

  useEffect(() => {
    let active = true;
    queueMicrotask(() => {
      if (active) {
        void load();
      }
    });
    return () => {
      active = false;
      controller.current?.abort();
      controller.current = null;
    };
  }, [load]);

  return { entry, failed, retry: load };
}
