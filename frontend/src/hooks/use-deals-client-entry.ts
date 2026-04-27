"use client";

import type { DealsTable } from "@/components/deals/DealsTable";
import { useStaffClientEntry } from "./use-staff-client-entry";
import { privateApiFetch } from "@/lib/api-browser";
import type { CurrentActor } from "@/lib/auth/actor";
import { isUuid } from "@/lib/auth/contracts";
import type { SearchParams } from "@/lib/query";
import { dealParams, dealPath, dealQuery, parseDeal, parseDealPage } from "@/lib/deals/deals";

export function useDealsClientEntry(actor: CurrentActor, parameters: SearchParams) {
  return useStaffClientEntry(async (signal) => {
    const query = dealParams(parameters, actor.role);
    const dealId = isUuid(parameters.deal) ? parameters.deal : "";
    const [initial, selected, { DealsTable: Table }] = await Promise.all([
      privateApiFetch(`/api/staff/deals?${dealQuery(query.filters, query.page)}`, parseDealPage, {
        signal,
      }),
      // A deal named in the URL is read with the list so its drawer opens populated.
      dealId ? privateApiFetch(dealPath(dealId), parseDeal, { signal }).catch(() => null) : null,
      import("@/components/deals/DealsTable"),
    ]);
    if (
      !initial.data ||
      initial.data.page !== query.page ||
      initial.data.pageSize !== query.filters.pageSize
    ) {
      throw new Error("Deals unavailable");
    }
    const confirmed = selected?.data?.id === dealId ? selected : null;
    return {
      ...query,
      Table: Table as typeof DealsTable,
      initial,
      dealId,
      selected: dealId
        ? (confirmed ?? { status: 502, data: null, message: "", uncertain: false })
        : null,
    };
  }, "Deals could not be loaded. Retry to open the workspace.");
}
