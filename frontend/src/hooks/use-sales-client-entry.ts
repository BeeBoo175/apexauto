"use client";

import type { SalesTable } from "@/components/sales/SalesTable";
import type { SalesWorkspace } from "@/components/sales/SalesWorkspace";
import { useStaffClientEntry } from "./use-staff-client-entry";
import { privateApiFetch } from "@/lib/api-browser";
import type { ApiResult } from "@/lib/api";
import type { CurrentActor } from "@/lib/auth/actor";
import { DEFAULT_PAGE_SIZE } from "@/lib/pagination";
import type { SearchParams } from "@/lib/query";
import { parseSale, salePath, type Sale } from "@/lib/sales/sale";
import {
  firstQueueView,
  isSalesManager,
  parseSalesQueue,
  parseSalesTable,
  queueParams,
  queueQuery,
  queueViews,
  saleSelection,
  salesQueuePath,
  salesTablePath,
  tableParams,
  tableQuery,
  type QueueView,
  type SalesQueue,
} from "@/lib/sales/sales";

const unavailable: ApiResult<Sale> = { status: 502, data: null, message: "", uncertain: false };

// A sale named in the address is read with the first screen so its editor opens populated.
async function readSelected(saleId: string, signal: AbortSignal) {
  if (!saleId) {
    return null;
  }
  const result = await privateApiFetch(salePath(saleId), parseSale, { signal }).catch(() => null);
  return result?.data?.id === saleId ? result : unavailable;
}

async function readQueue(view: QueueView, signal: AbortSignal) {
  const result = await privateApiFetch(`${salesQueuePath}?${queueQuery(view)}`, parseSalesQueue, {
    signal,
  });
  if (result.data && (result.data.page !== 1 || result.data.pageSize !== DEFAULT_PAGE_SIZE)) {
    throw new Error("Sales queue unavailable");
  }
  return result;
}

// One page, two surfaces: agents get the queue workspace, managers the Sales table.
export function useSalesClientEntry(actor: CurrentActor, parameters: SearchParams) {
  return useStaffClientEntry(async (signal) => {
    const selection = saleSelection(parameters);
    const common = {
      saleId: selection.saleId,
      section: selection.section,
      version: selection.version,
    };
    if (isSalesManager(actor)) {
      const query = tableParams(parameters);
      const [initial, selected, { SalesTable: Table }] = await Promise.all([
        privateApiFetch(
          `${salesTablePath}?${tableQuery(query.filters, query.page)}`,
          parseSalesTable,
          {
            signal,
          },
        ),
        readSelected(selection.saleId, signal),
        import("@/components/sales/SalesTable"),
      ]);
      if (
        !initial.data ||
        initial.data.page !== query.page ||
        initial.data.pageSize !== query.filters.pageSize
      ) {
        throw new Error("Sales unavailable");
      }
      return {
        kind: "manager" as const,
        Table: Table as typeof SalesTable,
        initial,
        ...query,
        ...common,
        selected,
      };
    }
    const [queues, selected, { SalesWorkspace: Workspace }] = await Promise.all([
      Promise.all(queueViews.map((view) => readQueue(view, signal))),
      readSelected(selection.saleId, signal),
      import("@/components/sales/SalesWorkspace"),
    ]);
    const byView = Object.fromEntries(
      queueViews.map((view, index) => [view, queues[index]]),
    ) as Record<QueueView, ApiResult<SalesQueue>>;
    return {
      kind: "agent" as const,
      Workspace: Workspace as typeof SalesWorkspace,
      queues: byView,
      // Sales in progress come first; with none, the agent is taken to the holds waiting to be claimed.
      initialView: firstQueueView(queueParams(parameters), byView.Handling.data?.counts),
      ...common,
      selected,
    };
  }, "Sales could not be loaded. Retry to open the workspace.");
}
