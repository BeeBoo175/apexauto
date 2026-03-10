"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import type { ApiResult } from "@/lib/api";
import { DEFAULT_PAGE_SIZE } from "@/lib/pagination";
import { inspectionFetch } from "@/lib/inspection/browser";
import { parseWorkshopJobs } from "@/lib/inspection/parse-workshop";
import { technicianQueueQuery } from "@/lib/inspection/query";
import type { TechnicianTask, WorkshopJobs } from "@/lib/inspection/workshop-contracts";
import { useInitialToast } from "./use-initial-toast";

type Read = { search: string; after: string | null; submittedDraft?: string };

export function useTechnicianQueue(
  task: TechnicianTask,
  initial: ApiResult<WorkshopJobs>,
  search: string,
) {
  const [entry, setEntry] = useState({ initial, snapshot: initial.data, search });
  const [draft, setDraft] = useState(search);
  const [failed, setFailed] = useState<Read | null>(null);
  const [busy, setBusy] = useState(false);
  const [ready, setReady] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const { error } = useToast();
  useInitialToast(initial.message);
  if (entry.initial !== initial) {
    setEntry({ initial, snapshot: initial.data, search });
    setDraft(search);
    setFailed(null);
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
    async (read: Read, notifyFailure = true) => {
      if (controller.current) {
        return false;
      }
      const attempt = new AbortController();
      controller.current = attempt;
      setBusy(true);
      setFailed(null);
      try {
        const result = await inspectionFetch(
          `workshop/jobs?${technicianQueueQuery(task, read.search, read.after)}`,
          parseWorkshopJobs,
          { signal: attempt.signal },
        );
        if (attempt.signal.aborted || controller.current !== attempt) {
          return false;
        }
        const page = result.data;
        if (
          result.status !== 200 ||
          !page ||
          page.after !== read.after ||
          page.pageSize !== DEFAULT_PAGE_SIZE ||
          (read.after !== null && page.nextCursor === read.after)
        ) {
          throw new Error("Worklist unavailable");
        }
        setEntry((current) => {
          if (current.initial !== initial) {
            return current;
          }
          const rows = read.after === null ? [] : (current.snapshot?.items ?? []);
          const items = new Map(rows.map((row) => [row.vehicleId, row]));
          for (const row of page.items) {
            items.set(row.vehicleId, row);
          }
          return {
            initial,
            search: read.search,
            snapshot: { ...page, items: [...items.values()] },
          };
        });
        if (read.submittedDraft !== undefined) {
          setDraft((current) => (current === read.submittedDraft ? read.search : current));
        }
        const url = new URL(window.location.href);
        url.searchParams.delete(`${task}.page`);
        url.searchParams.delete(`${task}.pageSize`);
        if (read.search) {
          url.searchParams.set(`${task}.search`, read.search);
        } else {
          url.searchParams.delete(`${task}.search`);
        }
        window.history.replaceState(null, "", url);
        return true;
      } catch {
        if (!attempt.signal.aborted && controller.current === attempt) {
          setFailed(read);
          if (notifyFailure) {
            error(
              "This worklist could not be loaded. Confirmed jobs and your selected work remain shown.",
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
    [task, initial, error],
  );

  function restart(search: string, notifyFailure = true, replaceDraft = false) {
    controller.current?.abort();
    controller.current = null;
    return load(
      {
        search: search.trim(),
        after: null,
        submittedDraft: replaceDraft ? draft : undefined,
      },
      notifyFailure,
    );
  }

  const loadMore = useCallback(() => {
    if (!entry.snapshot?.nextCursor || failed) {
      return;
    }
    return load({ search: entry.search, after: entry.snapshot.nextCursor });
  }, [entry, failed, load]);

  return {
    snapshot: entry.snapshot,
    search: entry.search,
    draft,
    setDraft,
    busy,
    ready,
    failed: Boolean(failed),
    loadMore,
    submit: () => restart(draft, true, true),
    clear: () => restart("", true, true),
    refresh: (notifyFailure = true) => restart(entry.search, notifyFailure),
    retry: () => (failed ? load(failed) : restart(entry.search)),
  };
}
