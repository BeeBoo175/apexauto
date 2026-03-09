"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { inspectionFetch } from "@/lib/inspection/browser";

type HistoryPage = { inspectionId: string; page: number; pageSize: number; totalCount: number };

export function useInspectionHistory<T extends HistoryPage>(
  initial: T | null,
  path: string,
  parse: (value: unknown) => T | null,
  failure: string,
  source?: { inspectionId: string; revision: number },
) {
  const [entry, setEntry] = useState({ initial, path, history: initial });
  const [busy, setBusy] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const { error } = useToast();
  const inspectionId = initial?.inspectionId ?? source?.inspectionId;
  const revision = initial && "revision" in initial ? initial.revision : source?.revision;
  if (entry.initial !== initial || entry.path !== path) {
    setEntry({ initial, path, history: initial });
    setBusy(false);
  }
  const history = entry.initial === initial && entry.path === path ? entry.history : initial;
  useEffect(
    () => () => {
      controller.current?.abort();
      controller.current = null;
    },
    [initial, path],
  );

  const load = useCallback(
    async (page: number, pageSize: number) => {
      if (controller.current || !inspectionId) {
        return;
      }
      const attempt = new AbortController();
      controller.current = attempt;
      setBusy(true);
      try {
        const result = await inspectionFetch(`${path}?page=${page}&pageSize=${pageSize}`, parse, {
          signal: attempt.signal,
        });
        if (attempt.signal.aborted || controller.current !== attempt) {
          return;
        }
        if (
          result.status !== 200 ||
          !result.data ||
          result.data.inspectionId !== inspectionId ||
          result.data.page !== page ||
          result.data.pageSize !== pageSize ||
          (revision !== undefined &&
            (!("revision" in result.data) || result.data.revision !== revision))
        ) {
          throw new Error("History unavailable");
        }
        const next = result.data;
        setEntry((current) =>
          current.initial === initial && current.path === path
            ? { ...current, history: next }
            : current,
        );
      } catch {
        if (!attempt.signal.aborted && controller.current === attempt) {
          error(failure);
        }
      } finally {
        if (controller.current === attempt) {
          controller.current = null;
          setBusy(false);
        }
      }
    },
    [inspectionId, revision, initial, path, parse, failure, error],
  );

  useEffect(() => {
    let active = true;
    queueMicrotask(() => {
      if (active && !initial && inspectionId) {
        void load(1, 25);
      }
    });
    return () => {
      active = false;
    };
  }, [initial, inspectionId, load]);

  return { history, busy, load };
}
