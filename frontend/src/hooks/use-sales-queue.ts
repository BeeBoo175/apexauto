"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { privateApiFetch } from "@/lib/api-browser";
import type { ApiResult } from "@/lib/api";
import { DEFAULT_PAGE_SIZE } from "@/lib/pagination";
import {
  parseSalesQueue,
  salesQuery,
  salesQueuePath,
  type SalesQueue,
  type SalesView,
} from "@/lib/sales/sales";
import { useInitialToast } from "./use-initial-toast";

// One tab of the sales queue: its first screen, more screens on demand, and a refresh that keeps what was confirmed.
export function useSalesQueue(view: SalesView, initial: ApiResult<SalesQueue>) {
  const [entry, setEntry] = useState({ initial, snapshot: initial.data });
  const [failedPage, setFailedPage] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);
  const [ready, setReady] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const { error } = useToast();
  useInitialToast(initial.message);
  if (entry.initial !== initial) {
    setEntry({ initial, snapshot: initial.data });
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
    async (page: number, notifyFailure = true) => {
      if (controller.current) {
        return false;
      }
      const attempt = new AbortController();
      controller.current = attempt;
      setBusy(true);
      setFailedPage(null);
      try {
        const result = await privateApiFetch(
          `${salesQueuePath}?${salesQuery(view, page)}`,
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
          if (current.initial !== initial) {
            return current;
          }
          const rows = page === 1 ? [] : (current.snapshot?.items ?? []);
          const items = new Map(rows.map((row) => [row.reservationId, row]));
          for (const row of next.items) {
            items.set(row.reservationId, row);
          }
          return { initial, snapshot: { ...next, items: [...items.values()] } };
        });
        return true;
      } catch {
        if (!attempt.signal.aborted && controller.current === attempt) {
          setFailedPage(page);
          if (notifyFailure) {
            error(
              "The sales queue could not be loaded. Confirmed holds and your selected deal remain shown.",
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

  const snapshot = entry.snapshot;
  const loaded = snapshot?.items.length ?? 0;
  const loadMore = useCallback(() => {
    if (!snapshot || failedPage !== null || loaded >= snapshot.total) {
      return;
    }
    return load(snapshot.page + 1);
  }, [snapshot, failedPage, loaded, load]);

  return {
    snapshot,
    busy,
    ready,
    failed: failedPage !== null,
    loadMore,
    refresh: (notifyFailure = true) => {
      controller.current?.abort();
      controller.current = null;
      return load(1, notifyFailure);
    },
    retry: () => load(failedPage ?? 1),
  };
}
