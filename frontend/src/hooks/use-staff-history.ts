"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import type { ApiRequest } from "@/lib/api";

export interface StaffHistoryPage {
  page: number;
  pageSize: number;
  totalCount: number;
}

type HistoryOptions = { enabled?: boolean; progressive?: boolean; attempted?: boolean };
interface HistoryResource<T> {
  path: string;
  parse: (value: unknown) => T | null;
  matches: (value: T) => boolean;
  append: (previous: T, next: T) => T;
  read: ApiRequest;
}

async function readHistoryPages<T extends StaffHistoryPage>(
  resource: HistoryResource<T>,
  paging: { page: number; pageSize: number; through: number },
  signal: AbortSignal,
  previous: T | null,
) {
  let history = previous;
  const { path, read, parse, matches, append } = resource;
  const { page, pageSize, through } = paging;
  for (let currentPage = page; currentPage <= through; currentPage += 1) {
    const result = await read(`${path}?page=${currentPage}&pageSize=${pageSize}`, parse, { signal });
    signal.throwIfAborted();
    if (
      result.status !== 200 ||
      !result.data ||
      !matches(result.data) ||
      result.data.page !== currentPage ||
      result.data.pageSize !== pageSize
    ) {
      throw new Error("History unavailable");
    }
    history = history ? append(history, result.data) : result.data;
  }
  return history;
}

export function useStaffHistory<T extends StaffHistoryPage>(
  initial: T | null,
  resource: HistoryResource<T>,
  failure: string,
  { enabled = true, progressive = false, attempted = false }: HistoryOptions = {},
) {
  const { path, parse, matches, append, read } = resource;
  const [entry, setEntry] = useState({
    initial,
    path,
    seedAttempted: attempted,
    history: initial,
    attempted: Boolean(initial) || attempted,
    failed: attempted && !initial,
  });
  const [busy, setBusy] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const retryRead = useRef({ page: 1, pageSize: 25, through: 1 });
  const { error } = useToast();
  if (entry.initial !== initial || entry.path !== path || entry.seedAttempted !== attempted) {
    const retain = progressive && entry.path === path && !initial;
    setEntry({
      initial,
      path,
      seedAttempted: attempted,
      history: retain ? entry.history : initial,
      attempted: Boolean(initial) || attempted || (retain && entry.attempted),
      failed: (attempted && !initial) || (retain && entry.failed),
    });
    setBusy(false);
  }
  const history = entry.initial === initial && entry.path === path ? entry.history : initial;
  useEffect(
    () => () => {
      controller.current?.abort();
      controller.current = null;
    },
    [initial, path, attempted],
  );

  const load = useCallback(
    async (page: number, pageSize: number, through = page) => {
      if (controller.current || !path) {
        return;
      }
      const attempt = new AbortController();
      const signal = AbortSignal.any([attempt.signal, AbortSignal.timeout(22_000)]);
      controller.current = attempt;
      retryRead.current = { page, pageSize, through };
      setBusy(true);
      setEntry((current) =>
        current.initial === initial && current.path === path
          ? { ...current, attempted: true, failed: false }
          : current,
      );
      try {
        const next = await readHistoryPages(
          { path, parse, matches, append, read },
          { page, pageSize, through },
          signal,
          progressive && page > 1 ? history : null,
        );
        if (attempt.signal.aborted || controller.current !== attempt) {
          return;
        }
        setEntry((current) =>
          current.initial === initial && current.path === path
            ? { ...current, history: next, failed: false }
            : current,
        );
      } catch {
        if (!attempt.signal.aborted && controller.current === attempt) {
          setEntry((current) =>
            current.initial === initial && current.path === path
              ? { ...current, failed: true }
              : current,
          );
          error(failure);
        }
      } finally {
        if (controller.current === attempt) {
          controller.current = null;
          setBusy(false);
        }
      }
    },
    [initial, path, parse, matches, append, read, failure, error, progressive, history],
  );

  useEffect(() => {
    let active = true;
    queueMicrotask(() => {
      if (active && enabled && !entry.attempted && path) {
        void load(1, 25);
      }
    });
    return () => {
      active = false;
    };
  }, [enabled, entry.attempted, path, load]);

  function refresh() {
    controller.current?.abort();
    controller.current = null;
    return load(1, history?.pageSize ?? 25, progressive ? (history?.page ?? 1) : 1);
  }

  function retry() {
    const { page, pageSize, through } = retryRead.current;
    return load(page, pageSize, through);
  }

  return {
    history,
    busy: busy || (enabled && !entry.attempted),
    attempted: entry.attempted,
    failed: entry.failed,
    load,
    refresh,
    retry,
  };
}
