"use client";

import type { SalesTable } from "@/components/sales/SalesTable";
import type { SalesWorkspace } from "@/components/sales/SalesWorkspace";
import { useStaffClientEntry } from "./use-staff-client-entry";
import { privateApiFetch } from "@/lib/api-browser";
import { apiReadError, unavailableResult, type ApiResult } from "@/lib/api";
import type { CurrentActor } from "@/lib/auth/actor";
import { DEFAULT_PAGE_SIZE } from "@/lib/pagination";
import type { SearchParams } from "@/lib/query";
import { parseSale, parseSalesQueue, parseSalesTable } from "@/lib/sales/parse";
import {
  salePath,
  firstQueueView,
  queueParams,
  queueQuery,
  saleSelection,
  salesQueuePath,
  salesTablePath,
  tableParams,
  tableQuery,
} from "@/lib/sales/query";
import { type Sale, isSalesManager, type QueueView, type SalesQueue } from "@/lib/sales/contracts";

// A sale named in the address is read with the first screen so its editor opens populated.
async function readSelected(saleId: string, signal: AbortSignal) {
  if (!saleId) {
    return null;
  }
  const result = await privateApiFetch(salePath(saleId), parseSale, { signal }).catch(() => null);
  return result?.data?.id === saleId ? result : unavailableResult<Sale>();
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
        throw apiReadError("Sales unavailable", initial);
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
    // One read carries every tab's count and the opening tab's first screen; other tabs load when shown.
    const requested = queueParams(parameters);
    const first = requested ?? "Handling";
    const [opening, selected, { SalesWorkspace: Workspace }] = await Promise.all([
      readQueue(first, signal),
      readSelected(selection.saleId, signal),
      import("@/components/sales/SalesWorkspace"),
    ]);
    // Sales in progress come first; with none, the agent is taken to the holds waiting to be claimed.
    const initialView = firstQueueView(requested, opening.data?.counts);
    const queues: Partial<Record<QueueView, ApiResult<SalesQueue>>> = { [first]: opening };
    if (initialView !== first) {
      queues[initialView] = await readQueue(initialView, signal);
    }
    return {
      kind: "agent" as const,
      Workspace: Workspace as typeof SalesWorkspace,
      queues,
      initialView,
      ...common,
      selected,
    };
  }, "Sales could not be loaded. Retry to open the workspace.");
}
