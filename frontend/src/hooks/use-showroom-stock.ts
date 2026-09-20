"use client";

import { useEffect, useState } from "react";
import { apiFetch } from "@/lib/api-browser";
import type { ShowroomBranchInfo } from "@/lib/guide/showrooms";
import { CATALOGUE_PAGE_SIZE } from "@/lib/vehicles/contracts";
import { parseCatalogue } from "@/lib/vehicles/parse";
import { catalogueDefaults, catalogueQuery } from "@/lib/vehicles/query";

export interface ShowroomStock {
  // False until the first read settles, so the page can hold the space the counts will take.
  ready: boolean;
  // Cars on sale at each showroom; a showroom whose read failed has no entry.
  stock: Readonly<Record<string, number>>;
}

// How many cars each showroom floor holds today: one catalogue read per showroom, in the browser.
export function useShowroomStock(showrooms: readonly ShowroomBranchInfo[]): ShowroomStock {
  const [state, setState] = useState<ShowroomStock>({ ready: false, stock: {} });
  const identity = showrooms.map((showroom) => showroom.id).join(",");

  useEffect(() => {
    const request = new AbortController();
    const ids = identity ? identity.split(",") : [];

    async function load() {
      const reads = await Promise.all(
        ids.map((branchId) =>
          apiFetch(
            `/api/vehicles?${catalogueQuery({ ...catalogueDefaults, branchId }, 1)}&pageSize=${CATALOGUE_PAGE_SIZE}`,
            parseCatalogue,
            { signal: request.signal },
          ).catch(() => null),
        ),
      );
      if (request.signal.aborted) {
        return;
      }
      const stock: Record<string, number> = {};
      ids.forEach((id, index) => {
        const total = reads[index]?.data?.total;
        if (total !== undefined) {
          stock[id] = total;
        }
      });
      setState({ ready: true, stock });
    }

    void load();
    return () => request.abort();
  }, [identity]);

  return state;
}
