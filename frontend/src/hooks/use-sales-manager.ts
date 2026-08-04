"use client";

import type { ApiResult } from "@/lib/api";
import type { Sale, SalesTableFilters, SalesTablePage } from "@/lib/sales/contracts";
import type { SaleSection } from "@/lib/sales/sale-sections";
import {
  idleAgentList,
  salesHref,
  salesLocation,
  salesWorkspace,
  type SaleSelectionPath,
  type SalesState,
} from "@/lib/sales/sales-location";
import { useGuardedAction } from "./use-guarded-action";
import { useSaleSelection } from "./use-sale-selection";
import { useSalesTable } from "./use-sales-table";
import { useSelectedRecord, useStaffAddress } from "./use-staff-address";

export interface SalesManagerEntry {
  initial: ApiResult<SalesTablePage>;
  filters: SalesTableFilters;
  page: number;
  saleId: string;
  section: SaleSection;
  version: number | null;
  selected: ApiResult<Sale> | null;
}

type SalesTarget = NonNullable<ReturnType<typeof salesLocation>>;

// The query that identifies the table shown beside the selection, so section-only traversals can be told apart.
function listKey(state: SalesState) {
  return salesHref({ ...state, id: "", section: "deal", version: null }, true);
}

// The managers' Sales workspace: the path selects the sale, section and past version, the query holds the table.
export function useSalesManager(entry: SalesManagerEntry) {
  const guard = useGuardedAction();
  const initial: SalesState = {
    id: entry.saleId,
    section: entry.section,
    version: entry.version,
    // The server names the tab it chose when the address left it open.
    filters: { ...entry.filters, view: entry.initial.data?.view ?? entry.filters.view },
    page: entry.page,
    agent: idleAgentList,
  };
  const address = useStaffAddress<SalesState, SalesTarget>({
    workspace: salesWorkspace,
    initial,
    href: (state) => salesHref(state, true),
    locate: (pathname, search) => salesLocation(pathname, search, true),
    synchronize,
  });
  const table = useSalesTable(entry.initial, entry.filters, entry.page, (filters, page, mode) =>
    address.update({ filters, page }, mode),
  );
  const drawer = useSaleSelection(
    entry.saleId,
    entry.selected,
    (selection, mode) => address.update(selection, mode),
    entry.section,
    entry.version,
  );
  useSelectedRecord(drawer.id);

  function tableState(): SalesState {
    return { ...initial, filters: table.active.filters, page: table.active.page };
  }

  function visitRecord(target: SaleSelectionPath) {
    if (target.id) {
      void drawer.visit(target);
    } else if (drawer.id || drawer.pendingId) {
      drawer.closeConfirmed(false);
    }
  }

  function synchronize(target: SalesTarget, previous: SalesTarget | null) {
    const key = listKey(target);
    if (previous && previous.id === target.id && listKey(previous) === key) {
      void drawer.visit(target);
      return;
    }
    table.cancel();
    if (key !== listKey(tableState())) {
      void table.load(target.filters, target.page, "replace", true);
    }
    visitRecord(target);
  }

  // Refresh follows the address: after a failed Back/Forward it reads the requested destination.
  function refresh() {
    const target = address.destination();
    const shown = target?.id === drawer.id && target.section === drawer.section;
    if (!target || (shown && listKey(target) === listKey(tableState()))) {
      return table.load(table.active.filters, table.active.page, "replace");
    }
    address.attempted(target);
    guard.request(() => {
      table.cancel();
      visitRecord(target);
      void table.load(target.filters, target.page, "replace", true);
    });
    return Promise.resolve(false);
  }

  return { table, drawer, refresh };
}
