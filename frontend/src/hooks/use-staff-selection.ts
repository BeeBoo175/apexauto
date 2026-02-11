"use client";
import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { intakeFetch } from "@/lib/intake/browser";
import type { ApiResult } from "@/lib/intake/contracts";
import type { AppointmentDetail } from "@/lib/staff/contracts";
import { parseDetail } from "@/lib/staff/parse";

export function useStaffSelection(initialId: string, initial: ApiResult<AppointmentDetail> | null) {
  const toast = useToast();
  const [id, setId] = useState(initialId);
  const [result, setResult] = useState(initial);
  const [busy, setBusy] = useState(false);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => () => controller.current?.abort(), []);

  async function choose(nextId: string) {
    controller.current?.abort();
    const attempt = new AbortController();
    controller.current = attempt;
    if (id !== nextId) {
      setResult(null);
    }
    setId(nextId);
    setBusy(true);
    const url = new URL(window.location.href);
    url.searchParams.set("appointment", nextId);
    window.history.replaceState(null, "", url);
    try {
      const response = await intakeFetch(`/api/staff/intake/appointments/${nextId}`, parseDetail, {
        signal: attempt.signal,
      });
      if (attempt.signal.aborted) {
        return;
      }
      if (response.data && response.data.id !== nextId) {
        throw new Error("Mismatched appointment");
      }
      if (!response.data || response.status !== 200) {
        throw new Error("Appointment unavailable");
      }
      setResult(response);
    } catch {
      if (!attempt.signal.aborted) {
        toast.error(
          "The appointment could not be refreshed. Existing details and your draft remain shown.",
        );
        setResult((current) => ({
          status: 502,
          data: current?.data ?? null,
          message: "",
          uncertain: true,
        }));
      }
    } finally {
      if (!attempt.signal.aborted) {
        setBusy(false);
      }
    }
  }
  function close() {
    controller.current?.abort();
    setId("");
    setResult(null);
    setBusy(false);
    const url = new URL(window.location.href);
    url.searchParams.delete("appointment");
    window.history.replaceState(null, "", url);
  }
  return { id, result, busy, choose, close };
}
