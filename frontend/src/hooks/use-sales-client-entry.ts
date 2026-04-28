"use client";

import type { SalesWorkspace } from "@/components/sales/SalesWorkspace";
import { useStaffClientEntry } from "./use-staff-client-entry";
import { privateApiFetch } from "@/lib/api-browser";
import type { ApiResult } from "@/lib/api";
import { DEFAULT_PAGE_SIZE } from "@/lib/pagination";
import type { SearchParams } from "@/lib/query";
import { dealPath, parseDeal, type Deal } from "@/lib/deals/deals";
import {
  firstView,
  parseSalesQueue,
  salesQuery,
  salesQueuePath,
  salesQueueParams,
  salesSelection,
  salesViews,
  type SalesQueue,
  type SalesView,
} from "@/lib/sales/sales";

async function readQueue(view: SalesView, signal: AbortSignal) {
  const initial = await privateApiFetch(`${salesQueuePath}?${salesQuery(view)}`, parseSalesQueue, {
    signal,
  });
  if (initial.data && (initial.data.page !== 1 || initial.data.pageSize !== DEFAULT_PAGE_SIZE)) {
    throw new Error("Sales queue unavailable");
  }
  return initial;
}

export function useSalesClientEntry(parameters: SearchParams) {
  return useStaffClientEntry(async (signal) => {
    const selection = salesSelection(parameters);
    const [queues, selected, { SalesWorkspace: Workspace }] = await Promise.all([
      Promise.all(salesViews.map((view) => readQueue(view, signal))),
      // A deal named in the address is read with the queues so its content opens populated.
      selection.dealId
        ? privateApiFetch(dealPath(selection.dealId), parseDeal, { signal }).catch(() => null)
        : null,
      import("@/components/sales/SalesWorkspace"),
    ]);
    const byView = Object.fromEntries(
      salesViews.map((view, index) => [view, queues[index]]),
    ) as Record<SalesView, ApiResult<SalesQueue>>;
    const confirmed = selected?.data?.id === selection.dealId ? selected : null;
    const unavailable: ApiResult<Deal> = { status: 502, data: null, message: "", uncertain: false };
    return {
      Workspace: Workspace as typeof SalesWorkspace,
      queues: byView,
      // Deals in progress come first; with none, the agent is taken to the holds waiting to be claimed.
      initialView: firstView(salesQueueParams(parameters), byView.Handling.data?.counts),
      selectedDealId: selection.dealId,
      selectedDeal: selection.dealId ? (confirmed ?? unavailable) : null,
      selectedSection: selection.section,
    };
  }, "The sales workspace could not be loaded. Retry to open it.");
}
