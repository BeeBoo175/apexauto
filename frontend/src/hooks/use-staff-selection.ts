"use client";
import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { apiFetch } from "@/lib/api-browser";
import type { ApiResult, ApiRequest } from "@/lib/api";

interface SelectionResource<T> {
  parameter: string;
  path: (id: string) => string;
  parse: (value: unknown) => T | null;
  matches: (value: T, id: string) => boolean;
  read?: ApiRequest;
  commitAfterRead?: boolean;
}

export function useStaffSelection<T>(
  initialId: string,
  initial: ApiResult<T> | null,
  resource: SelectionResource<T>,
) {
  const toast = useToast();
  const [id, setId] = useState(initialId);
  const [result, setResult] = useState(initial);
  const [busy, setBusy] = useState(false);
  const [pendingId, setPendingId] = useState("");
  const controller = useRef<AbortController | null>(null);
  useEffect(() => () => controller.current?.abort(), []);

  async function choose(nextId: string, onConfirmed?: (url: URL) => void) {
    controller.current?.abort();
    const attempt = new AbortController();
    controller.current = attempt;
    if (!resource.commitAfterRead) {
      if (id !== nextId) {
        setResult(null);
      }
      setId(nextId);
      const url = new URL(window.location.href);
      url.searchParams.set(resource.parameter, nextId);
      window.history.replaceState(null, "", url);
    }
    setBusy(true);
    setPendingId(nextId);
    try {
      const read = resource.read ?? apiFetch;
      const response = await read(resource.path(nextId), resource.parse, {
        signal: attempt.signal,
      });
      if (attempt.signal.aborted || controller.current !== attempt) {
        return;
      }
      if (response.data && !resource.matches(response.data, nextId)) {
        throw new Error("Mismatched record");
      }
      if (!response.data || response.status !== 200) {
        throw new Error("Record unavailable");
      }
      if (resource.commitAfterRead) {
        const url = new URL(window.location.href);
        url.searchParams.set(resource.parameter, nextId);
        onConfirmed?.(url);
        window.history.replaceState(null, "", url);
        setId(nextId);
      }
      setResult(response);
    } catch {
      if (!attempt.signal.aborted && controller.current === attempt) {
        toast.error(
          "The record could not be refreshed. Existing details and your draft remain shown.",
        );
        if (!resource.commitAfterRead) {
          setResult((current) => ({
            status: 502,
            data: current?.data ?? null,
            message: "",
            uncertain: true,
          }));
        }
      }
    } finally {
      if (!attempt.signal.aborted && controller.current === attempt) {
        controller.current = null;
        setBusy(false);
        setPendingId("");
      }
    }
  }
  function close() {
    controller.current?.abort();
    controller.current = null;
    setId("");
    setResult(null);
    setBusy(false);
    setPendingId("");
    const url = new URL(window.location.href);
    url.searchParams.delete(resource.parameter);
    window.history.replaceState(null, "", url);
  }
  return { id, result, busy, pendingId, choose, close };
}
