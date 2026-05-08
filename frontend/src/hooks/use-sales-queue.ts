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

// One tab of the agent's queue, or a search across all of them: its first screen, more on demand, and a refresh
// that keeps what was confirmed.
export function useSalesQueue(view: QueueView, initial: ApiResult<SalesQueue> | null, search = "") {
  const [entry, setEntry] = useState({ initial, snapshot: initial?.data ?? null, search });
  const [failedPage, setFailedPage] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);
  const [ready, setReady] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const { error } = useToast();
  useInitialToast(initial?.message ?? "");
  if (entry.initial !== initial) {
    setEntry({ initial, snapshot: initial?.data ?? null, search });
    setFailedPage(null);
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
    async (page: number, term: string, notifyFailure = true) => {
      if (controller.current) {
        return false;
      }
      const attempt = new AbortController();
      controller.current = attempt;
      setBusy(true);
      setFailedPage(null);
      try {
        const result = await privateApiFetch(
          `${salesQueuePath}?${queueQuery(view, term, page)}`,
          parseSalesQueue,
          { signal: attempt.signal },
        );
        if (attempt.signal.aborted || controller.current !== attempt) {
          return false;
        }
        const next = result.data;
        if (
          result.status !== 200 ||
          !next ||
          next.page !== page ||
          next.pageSize !== DEFAULT_PAGE_SIZE
        ) {
          throw new Error("Sales queue unavailable");
        }
        setEntry((current) => {
          const rows = page === 1 ? [] : (current.snapshot?.items ?? []);
          const items = new Map(rows.map((row) => [row.id, row]));
          for (const row of next.items) {
            items.set(row.id, row);
          }
          return { initial, snapshot: { ...next, items: [...items.values()] }, search: term };
        });
        return true;
      } catch {
        if (!attempt.signal.aborted && controller.current === attempt) {
          setFailedPage(page);
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
  const loaded = snapshot?.items.length ?? 0;
  const loadMore = useCallback(() => {
    if (!snapshot || failedPage !== null || loaded >= snapshot.total) {
      return;
    }
    return load(snapshot.page + 1, term);
  }, [snapshot, failedPage, loaded, load, term]);

  return {
    snapshot,
    search: term,
    busy,
    ready,
    failed: failedPage !== null,
    loadMore,
    find: (next: string) => {
      controller.current?.abort();
      controller.current = null;
      return load(1, next.trim());
    },
    refresh: (notifyFailure = true) => {
      controller.current?.abort();
      controller.current = null;
      return load(1, term, notifyFailure);
    },
    retry: () => load(failedPage ?? 1, term),
  };
}
