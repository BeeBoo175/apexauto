"use client";

import type { ListingTable } from "@/components/vehicles/ListingTable";
import { useStaffClientEntry } from "./use-staff-client-entry";
import { privateApiFetch } from "@/lib/api-browser";
import { type ApiResult, type Page } from "@/lib/api";
import type { CurrentActor } from "@/lib/auth/actor";
import { type Branch } from "@/lib/branches";
import type { SearchParams } from "@/lib/query";
import { listingParams, listingQuery, type ListingItem } from "@/lib/vehicles/listings";
import { parseListings } from "@/lib/vehicles/parse-listings";
import { parseListingWorkspace, type ListingWorkspace } from "@/lib/vehicles/listing-workspace";
import { readBranchChoices } from "@/lib/branch-choices";

interface ListingEntry extends ReturnType<typeof listingParams> {
  Table: typeof ListingTable;
  initial: ApiResult<Page<ListingItem>>;
  branches: Page<Branch> | null;
  selected: ApiResult<ListingWorkspace> | null;
}

export function useListingClientEntry(actor: CurrentActor, parameters: SearchParams) {
  return useStaffClientEntry<ListingEntry>(async (signal) => {
    const query = listingParams(parameters);
    const { filters, page, listingId } = query;

    const [initial, branches, selected, { ListingTable: Table }] = await Promise.all([
      privateApiFetch(`/api/staff/listings?${listingQuery(filters, page)}`, parseListings, {
        signal,
      }),
      actor.role === "GeneralManager" ? readBranchChoices(1, signal).catch(() => null) : null,
      listingId
        ? privateApiFetch(
            `/api/staff/listings/${listingId}`,
            (value) => {
              const workspace = parseListingWorkspace(value);
              return workspace?.vehicle.vehicleId === listingId ? workspace : null;
            },
            { signal },
          ).catch(() => null)
        : null,
      import("@/components/vehicles/ListingTable"),
    ]);
    if (
      initial.data &&
      (initial.data.page !== page || initial.data.pageSize !== filters.pageSize)
    ) {
      throw new Error("Listings unavailable");
    }
    return {
      ...query,
      Table,
      initial,
      branches: branches?.data ?? null,
      selected: listingId
        ? (selected ?? { status: 502, data: null, message: "", uncertain: false })
        : null,
    };
  }, "Listings could not be loaded. Retry to open the workspace.");
}
