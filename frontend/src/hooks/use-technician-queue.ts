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
import { writeAddress } from "@/lib/browser-url";

type Read = { search: string; page: number; pageSize: number; submittedDraft?: string };

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
          `workshop/jobs?${technicianQueueQuery(task, read.search, read.page, read.pageSize)}`,
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
          page.page !== read.page ||
          page.pageSize !== read.pageSize
        ) {
          throw new Error("Worklist unavailable");
        }
        setEntry((current) => {
          if (current.initial !== initial) {
            return current;
          }
          return {
            initial,
            search: read.search,
            snapshot: page,
          };
        });
        if (read.submittedDraft !== undefined) {
          setDraft((current) => (current === read.submittedDraft ? read.search : current));
        }
        const url = new URL(window.location.href);
        url.searchParams.set(`${task}.page`, String(read.page));
        url.searchParams.set(`${task}.pageSize`, String(read.pageSize));
        if (read.search) {
          url.searchParams.set(`${task}.search`, read.search);
        } else {
          url.searchParams.delete(`${task}.search`);
        }
        writeAddress(url);
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
        page: 1,
        pageSize: entry.snapshot?.pageSize ?? DEFAULT_PAGE_SIZE,
        submittedDraft: replaceDraft ? draft : undefined,
      },
      notifyFailure,
    );
  }

  return {
    snapshot: entry.snapshot,
    search: entry.search,
    draft,
    setDraft,
    busy,
    ready,
    failed: Boolean(failed),
    changePage: (page: number, pageSize: number) => load({ search: entry.search, page, pageSize }),
    submit: () => restart(draft, true, true),
    clear: () => restart("", true, true),
    refresh: (notifyFailure = true) => {
      controller.current?.abort();
      controller.current = null;
      return load(
        {
          search: entry.search,
          page: entry.snapshot?.page ?? 1,
          pageSize: entry.snapshot?.pageSize ?? DEFAULT_PAGE_SIZE,
        },
        notifyFailure,
      );
    },
    retry: () => (failed ? load(failed) : restart(entry.search)),
  };
}
