"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { useLoadingProgress, useLoadingProgressReady } from "@/components/ui/LoadingProgress";
import { privateApiFetch } from "@/lib/api-browser";
import { dateRangeError, type HistoryFilters } from "@/lib/intake/list-query";
import { writeAddress } from "@/lib/browser-url";

interface SearchableHistoryFilters extends HistoryFilters {
  // Absent for histories that are not searchable.
  search?: string;
}

interface HistoryPage {
  page: number;
}

interface AccountHistoryResource<T extends HistoryPage, F extends SearchableHistoryFilters> {
  path: (filters: F, page: number) => string;
  parse: (value: unknown) => T | null;
  // Extra checks beyond the confirmed page number, such as actor identity.
  confirms?: (history: T) => boolean;
  failureMessage: string;
  loadingLabel: string;
}

export function useAccountHistory<T extends HistoryPage, F extends SearchableHistoryFilters>(
  initialPage: number,
  initialFilters: F,
  resource: AccountHistoryResource<T, F>,
) {
  const [snapshot, setSnapshot] = useState<T | null>(null);
  const [page, setPage] = useState(initialPage);
  const [filters, setFilters] = useState(initialFilters);
  const [draft, setDraft] = useState(initialFilters);
  const [busy, setBusy] = useState(true);
  const controller = useRef<AbortController | null>(null);
  // The entry read mirrors the address that is already shown, so it must not rewrite it with defaults.
  const entryRead = useRef(true);
  const toast = useToast();
  useLoadingProgress(resource.loadingLabel, busy);
  useLoadingProgressReady(!busy);
  useEffect(() => () => controller.current?.abort(), []);
  useEffect(() => {
    // Every section reads its first page in the browser when it opens.
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function load(nextPage = page, nextFilters = filters) {
    const invalid = dateRangeError(nextFilters);
    if (invalid) {
      toast.error(invalid);
      return;
    }
    controller.current?.abort();
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    try {
      const result = await privateApiFetch(resource.path(nextFilters, nextPage), resource.parse, {
        signal: attempt.signal,
      });
      if (attempt.signal.aborted) {
        return;
      }
      if (
        result.status !== 200 ||
        !result.data ||
        result.data.page !== nextPage ||
        resource.confirms?.(result.data) === false
      ) {
        throw new Error("History unavailable");
      }
      setSnapshot(result.data);
      setPage(nextPage);
      setFilters(nextFilters);
      if (!entryRead.current) {
        writeHistoryUrl(nextPage, nextFilters);
      }
      entryRead.current = false;
    } catch {
      if (!attempt.signal.aborted) {
        toast.error(resource.failureMessage);
      }
    } finally {
      if (controller.current === attempt) {
        controller.current = null;
        setBusy(false);
      }
    }
  }

  function abort() {
    controller.current?.abort();
  }

  return { snapshot, setSnapshot, page, filters, draft, setDraft, busy, load, abort };
}

function writeHistoryUrl(page: number, filters: SearchableHistoryFilters) {
  const url = new URL(window.location.href);
  url.searchParams.set("page", String(page));
  for (const [key, value] of [
    ["from", filters.from],
    ["to", filters.to],
    ["search", filters.search?.trim() ?? ""],
  ]) {
    if (value) {
      url.searchParams.set(key, value);
    } else {
      url.searchParams.delete(key);
    }
  }
  writeAddress(`${url.pathname}${url.search}${url.hash}`);
}
