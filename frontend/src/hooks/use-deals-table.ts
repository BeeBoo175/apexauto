"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { privateApiFetch } from "@/lib/api-browser";
import type { ApiResult } from "@/lib/api";
import { dealQuery, parseDealPage, type DealFilters } from "@/lib/deals/deals";

export type DealPageData = NonNullable<ReturnType<typeof parseDealPage>>;

export function useDealsTable(
  initial: ApiResult<DealPageData>,
  filters: DealFilters,
  page: number,
) {
  const [snapshot, setSnapshot] = useState(initial.data);
  const [active, setActive] = useState({ filters, page });
  const [busy, setBusy] = useState(false);
  const [ready, setReady] = useState(false);
  const [revision, setRevision] = useState(0);
  const controller = useRef<AbortController | null>(null);
  const toast = useToast();

  useEffect(() => {
    queueMicrotask(() => setReady(true));
    return () => {
      controller.current?.abort();
      controller.current = null;
    };
  }, []);

  // A failed read keeps the last confirmed rows, filters and sort on screen.
  async function load(nextFilters = active.filters, nextPage = active.page) {
    if (controller.current) {
      return false;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    try {
      const result = await privateApiFetch(
        `/api/staff/deals?${dealQuery(nextFilters, nextPage)}`,
        parseDealPage,
        { signal: attempt.signal },
      );
      if (attempt.signal.aborted || controller.current !== attempt) {
        return false;
      }
      const data = result.data;
      if (
        result.status !== 200 ||
        !data ||
        data.page !== nextPage ||
        data.pageSize !== nextFilters.pageSize
      ) {
        throw new Error("Deals unavailable");
      }
      setSnapshot(data);
      setActive({ filters: nextFilters, page: nextPage });
      setRevision((value) => value + 1);
      const url = new URL(window.location.href);
      const selected = url.searchParams.get("deal");
      url.search = dealQuery(nextFilters, nextPage);
      if (selected) {
        url.searchParams.set("deal", selected);
      }
      window.history.replaceState(null, "", url);
      return true;
    } catch {
      if (!attempt.signal.aborted) {
        toast.error(
          "Deals could not be refreshed. The last confirmed rows and filters remain shown.",
        );
      }
      return false;
    } finally {
      if (controller.current === attempt) {
        controller.current = null;
        setBusy(false);
      }
    }
  }

  return { snapshot, active, busy, ready, revision, load };
}
