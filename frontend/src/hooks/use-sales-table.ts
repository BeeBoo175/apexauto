"use client";

import { useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { privateApiFetch } from "@/lib/api-browser";
import type { ApiResult } from "@/lib/api";
import { parseSalesTable } from "@/lib/sales/parse";
import { salesTablePath, tableQuery } from "@/lib/sales/query";
import type { SalesTableFilters, SalesTablePage } from "@/lib/sales/contracts";
import { useBoundedRead } from "./use-bounded-read";
import { writeAddress } from "@/lib/browser-url";

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
  const [revision, setRevision] = useState(0);
  const { busy, ready, begin, isCurrent, end } = useBoundedRead();
  const toast = useToast();

  async function load(nextFilters = active.filters, nextPage = active.page) {
    const attempt = begin();
    if (!attempt) {
      return false;
    }
    try {
      const result = await privateApiFetch(
        `${salesTablePath}?${tableQuery(nextFilters, nextPage)}`,
        parseSalesTable,
        { signal: attempt.signal },
      );
      if (!isCurrent(attempt)) {
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
      writeAddress(url);
      return true;
    } catch {
      if (!attempt.signal.aborted) {
        toast.error(
          "Sales could not be refreshed. The last confirmed rows and filters remain shown.",
        );
      }
      return false;
    } finally {
      end(attempt);
    }
  }

  return { snapshot, active, busy, ready, revision, load };
}
