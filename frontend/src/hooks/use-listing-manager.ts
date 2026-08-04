"use client";

import type { ApiResult, Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import type { ListingRecordSelection } from "@/lib/vehicles/listing-history-selection";
import {
  listingHref,
  listingLocation,
  listingWorkspace,
  type ListingSelectionPath,
  type ListingState,
} from "@/lib/vehicles/listing-location";
import type { ListingWorkspace } from "@/lib/vehicles/listing-workspace";
import type { ListingFilters, ListingItem, ListingSection } from "@/lib/vehicles/listings";
import { useGuardedAction } from "./use-guarded-action";
import { useListingSelection } from "./use-listing-selection";
import { useListingTable } from "./use-listing-table";
import { useSelectedRecord, useStaffAddress } from "./use-staff-address";

export interface ListingManagerEntry {
  initial: ApiResult<Page<ListingItem>>;
  filters: ListingFilters;
  page: number;
  branches: Page<Branch> | null;
  listingId: string;
  selected: ApiResult<ListingWorkspace> | null;
  section: ListingSection;
  record: ListingRecordSelection | null;
}

type ListingTarget = NonNullable<ReturnType<typeof listingLocation>>;

// The query that identifies the table shown beside the selection, so section-only traversals can be told apart.
function listKey(state: ListingState) {
  return listingHref({ ...state, id: "", section: "listing", record: null });
}

// The Listings workspace: the path selects the listing, section and history record, the query holds the table.
export function useListingManager(entry: ListingManagerEntry) {
  const guard = useGuardedAction();
  const initial: ListingState = {
    id: entry.listingId,
    section: entry.section,
    record: entry.record,
    filters: entry.filters,
    page: entry.page,
  };
  const address = useStaffAddress<ListingState, ListingTarget>({
    workspace: listingWorkspace,
    initial,
    href: listingHref,
    locate: listingLocation,
    synchronize,
  });
  const table = useListingTable(
    entry.initial,
    entry.filters,
    entry.page,
    entry.branches,
    (filters, page, mode) => address.update({ filters, page }, mode),
  );
  const drawer = useListingSelection(
    entry.listingId,
    entry.selected,
    (selection, mode) => address.update(selection, mode),
    entry.section,
    entry.record,
  );
  useSelectedRecord(drawer.id);

  function tableState(): ListingState {
    return { ...initial, filters: table.active.filters, page: table.active.page };
  }

  function visitRecord(target: ListingSelectionPath) {
    if (target.id) {
      void drawer.visit(target);
    } else if (drawer.id || drawer.pendingId) {
      drawer.closeConfirmed(false);
    }
  }

  function synchronize(target: ListingTarget, previous: ListingTarget | null) {
    const key = listKey(target);
    if (previous && previous.id === target.id && listKey(previous) === key) {
      void drawer.visit(target);
      return;
    }
    table.cancel();
    if (key !== listKey(tableState())) {
      void table.load(target.filters, target.page, true, "replace", true);
    }
    visitRecord(target);
  }

  // Refresh follows the address: after a failed Back/Forward it reads the requested destination.
  function refresh() {
    const target = address.destination();
    const shown = target?.id === drawer.id && target.section === drawer.section;
    if (!target || (shown && listKey(target) === listKey(tableState()))) {
      return table.load(undefined, undefined, false, "replace");
    }
    address.attempted(target);
    guard.request(() => {
      table.cancel();
      visitRecord(target);
      void table.load(target.filters, target.page, true, "replace", true);
    });
    return Promise.resolve(false);
  }

  return { table, drawer, refresh };
}
