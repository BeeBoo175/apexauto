"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { privateApiFetch } from "@/lib/api-browser";
import type { ApiResult } from "@/lib/api";
import { dateRangeError, type HistoryFilters } from "@/lib/intake/list-query";
import { useInitialToast } from "./use-initial-toast";
import { writeAddress } from "@/lib/browser-url";

interface SearchableHistoryFilters extends HistoryFilters {
  search: string;
}

interface HistoryPage {
  page: number;
}

interface AccountHistoryResource<T extends HistoryPage, F extends SearchableHistoryFilters> {
  path: (filters: F, page: number) => string;
  parse: (value: unknown) => T | null;
  // Extra checks beyond the confirmed page number, such as actor identity.
  confirms?: (history: T) => boolean;
  // URL parameter prefix, such as "reservation" for reservationPage and reservationFrom.
  prefix: string;
  failureMessage: string;
}

export function useAccountHistory<T extends HistoryPage, F extends SearchableHistoryFilters>(
  initial: ApiResult<T>,
  initialPage: number,
  initialFilters: F,
  resource: AccountHistoryResource<T, F>,
) {
  const [snapshot, setSnapshot] = useState(initial.data);
  const [page, setPage] = useState(initialPage);
  const [filters, setFilters] = useState(initialFilters);
  const [draft, setDraft] = useState(initialFilters);
  const [busy, setBusy] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const toast = useToast();
  useInitialToast(initial.message);
  useEffect(() => () => controller.current?.abort(), []);

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
      writeHistoryUrl(resource.prefix, nextPage, nextFilters);
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

function writeHistoryUrl(prefix: string, page: number, filters: SearchableHistoryFilters) {
  const url = new URL(window.location.href);
  url.searchParams.set(`${prefix}Page`, String(page));
  for (const [key, value] of [
    [`${prefix}From`, filters.from],
    [`${prefix}To`, filters.to],
    [`${prefix}Search`, filters.search.trim()],
  ]) {
    if (value) {
      url.searchParams.set(key, value);
    } else {
      url.searchParams.delete(key);
    }
  }
  writeAddress(`${url.pathname}${url.search}${url.hash}`);
}
