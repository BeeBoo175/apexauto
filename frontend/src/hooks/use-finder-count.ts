"use client";

import { useEffect, useRef, useState } from "react";
import { apiFetch } from "@/lib/api-browser";
import type { CatalogueFilters } from "@/lib/vehicles/contracts";
import { parseCatalogue } from "@/lib/vehicles/parse";
import { catalogueQuery } from "@/lib/vehicles/query";

const COUNT_DELAY_MS = 250;

// The count behind the finder button. The server snapshot supplies the unfiltered total; a filtered count is read
// once the visitor pauses, and a failed read leaves the last confirmed figure in place.
export function useFinderCount(filters: CatalogueFilters, initialTotal: number | null) {
  const [total, setTotal] = useState<number | null>(initialTotal);
  const confirmedFilters = useRef(JSON.stringify(filters));
  const filtersKey = JSON.stringify(filters);

  useEffect(() => {
    if (filtersKey === confirmedFilters.current && initialTotal !== null) {
      return;
    }

    const attempt = new AbortController();
    const timer = window.setTimeout(async () => {
      const result = await apiFetch(`/api/vehicles?${catalogueQuery(filters, 1)}`, parseCatalogue, {
        signal: attempt.signal,
      });
      if (attempt.signal.aborted || !result.data) {
        return;
      }
      setTotal(result.data.total);
    }, COUNT_DELAY_MS);

    return () => {
      window.clearTimeout(timer);
      attempt.abort();
    };
    // The filters object is rebuilt on every render; its serialised form is the dependency.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filtersKey, initialTotal]);

  return total;
}
