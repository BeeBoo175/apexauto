"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { privateApiFetch } from "@/lib/api-browser";
import type { ApiResult } from "@/lib/api";
import { DEFAULT_PAGE_SIZE } from "@/lib/pagination";
import { parseSalesQueue } from "@/lib/sales/parse";
import { queueQuery, salesQueuePath } from "@/lib/sales/query";
import type { QueueView, SalesQueue } from "@/lib/sales/contracts";
import { useInitialToast } from "./use-initial-toast";

// Failed page replacements retain confirmed sales and the selected editor.
export function useSalesQueue(view: QueueView, initial: ApiResult<SalesQueue> | null, search = "") {
  const [entry, setEntry] = useState({ initial, snapshot: initial?.data ?? null, search });
  const [failedRead, setFailedRead] = useState<{
    page: number;
    pageSize: number;
    term: string;
  } | null>(null);
  const [busy, setBusy] = useState(false);
  const [ready, setReady] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const { error } = useToast();
  useInitialToast(initial?.message ?? "");
  if (entry.initial !== initial) {
    setEntry({ initial, snapshot: initial?.data ?? null, search });
    setFailedRead(null);
    setBusy(false);
  }
  useEffect(() => {
    queueMicrotask(() => setReady(true));
    return () => {
      controller.current?.abort();
      controller.current = null;
    };
  }, [initial]);

  const load = useCallback(
    async (page: number, term: string, pageSize = DEFAULT_PAGE_SIZE, notifyFailure = true) => {
      if (controller.current) {
        return false;
      }
      const attempt = new AbortController();
      controller.current = attempt;
      setBusy(true);
      setFailedRead(null);
      try {
        const result = await privateApiFetch(
          `${salesQueuePath}?${queueQuery(view, term, page, pageSize)}`,
          parseSalesQueue,
          { signal: attempt.signal },
        );
        if (attempt.signal.aborted || controller.current !== attempt) {
          return false;
        }
        const next = result.data;
        if (result.status !== 200 || !next || next.page !== page || next.pageSize !== pageSize) {
          throw new Error("Sales queue unavailable");
        }
        setEntry({ initial, snapshot: next, search: term });
        return true;
      } catch {
        if (!attempt.signal.aborted && controller.current === attempt) {
          setFailedRead({ page, pageSize, term });
          if (notifyFailure) {
            error(
              "The sales queue could not be loaded. Confirmed sales and your selected sale remain shown.",
            );
          }
        }
        return false;
      } finally {
        if (controller.current === attempt) {
          controller.current = null;
          setBusy(false);
        }
      }
    },
    [view, initial, error],
  );

  const { snapshot, search: term } = entry;

  return {
    snapshot,
    search: term,
    busy,
    ready,
    failed: failedRead !== null,
    changePage: (page: number, pageSize: number) => load(page, term, pageSize),
    find: (next: string) => {
      controller.current?.abort();
      controller.current = null;
      return load(1, next.trim(), snapshot?.pageSize);
    },
    refresh: (notifyFailure = true) => {
      controller.current?.abort();
      controller.current = null;
      return load(snapshot?.page ?? 1, term, snapshot?.pageSize, notifyFailure);
    },
    retry: () =>
      load(
        failedRead?.page ?? snapshot?.page ?? 1,
        failedRead?.term ?? term,
        failedRead?.pageSize ?? snapshot?.pageSize,
      ),
  };
}
