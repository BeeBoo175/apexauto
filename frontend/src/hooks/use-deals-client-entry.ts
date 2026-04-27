"use client";

import type { DealsTable } from "@/components/deals/DealsTable";
import { useStaffClientEntry } from "./use-staff-client-entry";
import { privateApiFetch } from "@/lib/api-browser";
import type { CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";
import { dealParams, dealQuery, parseDealPage } from "@/lib/deals/deals";

export function useDealsClientEntry(actor: CurrentActor, parameters: SearchParams) {
  return useStaffClientEntry(async (signal) => {
    const query = dealParams(parameters, actor.role);
    const [initial, { DealsTable: Table }] = await Promise.all([
      privateApiFetch(`/api/staff/deals?${dealQuery(query.filters, query.page)}`, parseDealPage, {
        signal,
      }),
      import("@/components/deals/DealsTable"),
    ]);
    if (
      !initial.data ||
      initial.data.page !== query.page ||
      initial.data.pageSize !== query.filters.pageSize
    ) {
      throw new Error("Deals unavailable");
    }
    return { ...query, Table: Table as typeof DealsTable, initial };
  }, "Deals could not be loaded. Retry to open the workspace.");
}
