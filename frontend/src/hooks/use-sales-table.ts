"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { privateApiFetch } from "@/lib/api-browser";
import type { ApiResult } from "@/lib/api";
import { parseSalesTable } from "@/lib/sales/parse";
import { salesTablePath, tableQuery } from "@/lib/sales/query";
import type { SalesTableFilters, SalesTablePage } from "@/lib/sales/contracts";

// The managers' Sales table: one bounded read per view, filter, sort or page, keeping the last confirmed rows.
export function useSalesTable(
  initial: ApiResult<SalesTablePage>,
  filters: SalesTableFilters,
  page: number,
) {
  const [snapshot, setSnapshot] = useState(initial.data);
  // The server names the view it chose when the address left it open, so later reads stay on that tab.
  const [active, setActive] = useState({
    filters: { ...filters, view: initial.data?.view ?? filters.view },
    page,
  });
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

  async function load(nextFilters = active.filters, nextPage = active.page) {
    if (controller.current) {
      return false;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    try {
      const result = await privateApiFetch(
        `${salesTablePath}?${tableQuery(nextFilters, nextPage)}`,
        parseSalesTable,
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
        throw new Error("Sales unavailable");
      }
      const confirmed = { ...nextFilters, view: data.view };
      setSnapshot(data);
      setActive({ filters: confirmed, page: nextPage });
      setRevision((value) => value + 1);
      const url = new URL(window.location.href);
      const kept = ["sale", "section", "version"].map(
        (name) => [name, url.searchParams.get(name)] as const,
      );
      url.search = tableQuery(confirmed, nextPage);
      for (const [name, value] of kept) {
        if (value) {
          url.searchParams.set(name, value);
        }
      }
      window.history.replaceState(null, "", url);
      return true;
    } catch {
      if (!attempt.signal.aborted) {
        toast.error(
          "Sales could not be refreshed. The last confirmed rows and filters remain shown.",
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
