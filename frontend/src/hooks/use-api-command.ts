"use client";
import { useEffect, useRef, useState } from "react";
import { apiFetch } from "@/lib/api-browser";
import type { ApiResult, ApiRequest } from "@/lib/api";
import { useToast } from "@/components/ui/Toast";

// Throttling a replay does not resolve the earlier unconfirmed write.
export function isUnconfirmedResult(result: ApiResult<unknown>, replaying: boolean) {
  return result.uncertain || result.status >= 500 || (replaying && result.status === 429);
}

export function useApiCommand<T>(
  path: string,
  parse: (value: unknown) => T | null,
  matches: (receipt: T, request: unknown) => boolean = () => true,
  fetchCommand: ApiRequest = apiFetch,
) {
  const [busy, setBusy] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  const [receipt, setReceipt] = useState<T | null>(null);
  const attempt = useRef<string | null>(null);
  const controller = useRef<AbortController | null>(null);
  // Set just before the page itself navigates away on purpose, such as to a payment page.
  const leaving = useRef(false);
  const { success, error, warning } = useToast();

  useEffect(() => () => controller.current?.abort(), []);
  useEffect(() => {
    if (!busy && !uncertain) {
      return;
    }
    const guard = (event: BeforeUnloadEvent) => {
      if (!leaving.current) {
        event.preventDefault();
      }
    };
    window.addEventListener("beforeunload", guard);
    return () => window.removeEventListener("beforeunload", guard);
  }, [busy, uncertain]);

  async function submit(body: unknown): Promise<ApiResult<T> | null> {
    if (controller.current) {
      return null;
    }
    leaving.current = false;
    // Reconciliation must replay the captured payload and operation ID, even if the form changes.
    attempt.current ??= JSON.stringify(body);
    const operation = new AbortController();
    controller.current = operation;
    setBusy(true);
    try {
      const result = await fetchCommand(path, parse, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: attempt.current,
        signal: operation.signal,
      });
      if (operation.signal.aborted) {
        return null;
      }
      if (isUnconfirmedResult(result, uncertain)) {
        throw new Error("Unconfirmed mutation");
      }
      if (
        result.status === 200 &&
        result.data &&
        !matches(result.data, JSON.parse(attempt.current))
      ) {
        throw new Error("Receipt does not match the captured operation");
      }
      attempt.current = null;
      setUncertain(false);
      if (result.status === 200 && result.data) {
        setReceipt(result.data);
        success("Request confirmed.");
      } else {
        error(result.message);
      }
      return result;
    } catch {
      if (!operation.signal.aborted) {
        setUncertain(true);
        warning("The result is unconfirmed. Retry the same request to reconcile it.");
      }
      return null;
    } finally {
      if (!operation.signal.aborted) {
        setBusy(false);
      }
      controller.current = null;
    }
  }

  // The command has finished and the page is about to move on by itself. The guard stays attached until React
  // re-renders after the command settles, so without this the browser warns about the page's own redirect.
  function allowLeave() {
    leaving.current = true;
  }

  return { busy, uncertain, receipt, submit, allowLeave };
}
