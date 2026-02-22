"use client";
import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { intakeFetch } from "@/lib/intake/browser";
import type { ApiResult } from "@/lib/intake/contracts";

interface SelectionResource<T> {
  parameter: string;
  path: (id: string) => string;
  parse: (value: unknown) => T | null;
  matches: (value: T, id: string) => boolean;
  read?: typeof intakeFetch;
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
  const controller = useRef<AbortController | null>(null);
  useEffect(() => () => controller.current?.abort(), []);

  async function choose(nextId: string) {
    controller.current?.abort();
    const attempt = new AbortController();
    controller.current = attempt;
    // Retain confirmed details only when refreshing the same record.
    if (id !== nextId) {
      setResult(null);
    }
    setId(nextId);
    setBusy(true);
    const url = new URL(window.location.href);
    url.searchParams.set(resource.parameter, nextId);
    window.history.replaceState(null, "", url);
    try {
      const read = resource.read ?? intakeFetch;
      const response = await read(resource.path(nextId), resource.parse, {
        signal: attempt.signal,
      });
      if (attempt.signal.aborted) {
        return;
      }
      if (response.data && !resource.matches(response.data, nextId)) {
        throw new Error("Mismatched record");
      }
      if (!response.data || response.status !== 200) {
        throw new Error("Record unavailable");
      }
      setResult(response);
    } catch {
      if (!attempt.signal.aborted) {
        toast.error(
          "The record could not be refreshed. Existing details and your draft remain shown.",
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
    url.searchParams.delete(resource.parameter);
    window.history.replaceState(null, "", url);
  }
  return { id, result, busy, choose, close };
}
