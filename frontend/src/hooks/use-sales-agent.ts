"use client";

import { useState } from "react";
import { useToast } from "@/components/ui/Toast";
import type { ApiResult } from "@/lib/api";
import { queueViews, type QueueView, type Sale, type SalesQueue } from "@/lib/sales/contracts";
import type { SaleSection } from "@/lib/sales/sale-sections";
import {
  idleAgentList,
  salesHref,
  salesLocation,
  salesWorkspace,
  type AgentList,
  type SaleSelectionPath,
  type SalesState,
} from "@/lib/sales/sales-location";
import { defaultTableFilters } from "@/lib/sales/query";
import { useGuardedAction } from "./use-guarded-action";
import { useSaleSelection } from "./use-sale-selection";
import { useSalesQueue, type SalesQueueRead } from "./use-sales-queue";
import { useSelectedRecord, useStaffAddress, type AddressMode } from "./use-staff-address";

export interface SalesAgentEntry {
  // The opening tab is read on entry; the others are read the first time they are shown.
  queues: Partial<Record<QueueView, ApiResult<SalesQueue>>>;
  found: ApiResult<SalesQueue> | null;
  list: AgentList & { view: QueueView };
  selectedSaleId: string;
  selectedSale: ApiResult<Sale> | null;
  selectedSection: SaleSection;
  selectedVersion: number | null;
}

type SalesTarget = NonNullable<ReturnType<typeof salesLocation>>;

// The tab and search that identify the list shown beside the selection.
function listKey(state: SalesState) {
  return salesHref({ ...state, id: "", section: "deal", version: null }, false);
}

// The agent's Sales workspace: the path selects the sale, section and past version, the query holds the open
// queue tab and, for an applied search, its results.
export function useSalesAgent(entry: SalesAgentEntry) {
  const toast = useToast();
  const guard = useGuardedAction();
  const [view, setView] = useState(entry.list.view);
  const initial: SalesState = {
    id: entry.selectedSaleId,
    section: entry.selectedSection,
    version: entry.selectedVersion,
    filters: defaultTableFilters(),
    page: 1,
    agent: entry.list,
  };
  const address = useStaffAddress<SalesState, SalesTarget>({
    workspace: salesWorkspace,
    initial,
    href: (state) => salesHref(state, false),
    locate: (pathname, search) => salesLocation(pathname, search, false),
    synchronize,
  });

  function writeList(change: Partial<AgentList>, mode: AddressMode) {
    address.update({ agent: { ...address.confirmed.current.agent, ...change } }, mode);
  }

  // Only the list the address names writes to it; refreshing another loaded tab leaves the address alone.
  function viewLocation(tab: QueueView) {
    return ({ page, pageSize }: SalesQueueRead, mode: AddressMode) => {
      const { view: shown, search } = address.confirmed.current.agent;
      if (shown === tab && !search) {
        writeList({ page, pageSize }, mode);
      }
    };
  }

  const handling = useSalesQueue(
    "Handling",
    entry.queues.Handling ?? null,
    "",
    viewLocation("Handling"),
  );
  const waiting = useSalesQueue(
    "Waiting",
    entry.queues.Waiting ?? null,
    "",
    viewLocation("Waiting"),
  );
  const settling = useSalesQueue(
    "Settling",
    entry.queues.Settling ?? null,
    "",
    viewLocation("Settling"),
  );
  const completed = useSalesQueue(
    "Completed",
    entry.queues.Completed ?? null,
    "",
    viewLocation("Completed"),
  );
  const claimable = useSalesQueue(
    "ToClaim",
    entry.queues.ToClaim ?? null,
    "",
    viewLocation("ToClaim"),
  );
  const worklists = {
    Handling: handling,
    Waiting: waiting,
    Settling: settling,
    Completed: completed,
    ToClaim: claimable,
  };
  const found = useSalesQueue(
    "ToClaim",
    entry.found,
    entry.list.search,
    ({ page, pageSize, term }, mode) => {
      if (term) {
        writeList({ search: term, page, pageSize }, mode);
        return;
      }
      const shown = worklists[address.confirmed.current.agent.view || "Handling"].snapshot;
      writeList(
        {
          search: "",
          page: shown?.page ?? 1,
          pageSize: shown?.pageSize ?? idleAgentList.pageSize,
        },
        mode,
      );
    },
  );
  const drawer = useSaleSelection(
    entry.selectedSaleId,
    entry.selectedSale,
    (selection, mode) => address.update(selection, mode),
    entry.selectedSection,
    entry.selectedVersion,
  );
  useSelectedRecord(drawer.id);

  function showView(value: QueueView, mode: AddressMode = "push") {
    setView(value);
    const snapshot = worklists[value].snapshot;
    if (!snapshot) {
      void worklists[value].refresh();
    }
    writeList(
      {
        view: value,
        search: "",
        page: snapshot?.page ?? 1,
        pageSize: snapshot?.pageSize ?? idleAgentList.pageSize,
      },
      mode,
    );
  }

  function visitRecord(target: SaleSelectionPath) {
    if (target.id) {
      void drawer.visit(target);
    } else if (drawer.id || drawer.pendingId) {
      drawer.closeConfirmed(false);
    }
  }

  // Reads the list a destination names when it is not the one shown.
  function visitList(target: SalesTarget) {
    const { view: wanted, search, page, pageSize } = target.agent;
    const tab = wanted || view;
    setView(tab);
    address.update({ agent: { ...target.agent, view: tab } }, false);
    const shown = search ? found : worklists[tab];
    const same =
      shown.snapshot?.page === page &&
      shown.snapshot.pageSize === pageSize &&
      shown.search === search;
    if (!same) {
      void shown.visit({ page, pageSize, term: search });
    }
    if (!worklists[tab].snapshot) {
      void worklists[tab].visit({ page: 1, pageSize: idleAgentList.pageSize, term: "" });
    }
  }

  function synchronize(target: SalesTarget, previous: SalesTarget | null) {
    if (previous && previous.id === target.id && listKey(previous) === listKey(target)) {
      void drawer.visit(target);
      return;
    }
    visitList(target);
    visitRecord(target);
  }

  async function refreshQueues() {
    const loaded = queueViews.filter((item) => item === view || worklists[item].snapshot);
    const reads = loaded.map((item) => worklists[item].refresh(false));
    if (found.search) {
      reads.push(found.refresh(false));
    }
    if ((await Promise.all(reads)).some((result) => result === false)) {
      toast.error(
        "The sales queues could not all be refreshed. The last confirmed queues remain shown.",
      );
    }
  }

  // Refresh follows the address: after a failed Back/Forward it reads the requested destination.
  function refreshDestination() {
    const target = address.destination();
    const shownList = listKey({ ...initial, agent: address.confirmed.current.agent });
    const shown = target?.id === drawer.id && target.section === drawer.section;
    if (!target || (shown && listKey(target) === shownList)) {
      return refreshQueues();
    }
    address.attempted(target);
    guard.request(() => {
      visitList(target);
      visitRecord(target);
    });
    return Promise.resolve();
  }

  return { view, showView, worklists, found, drawer, refreshQueues, refreshDestination };
}
