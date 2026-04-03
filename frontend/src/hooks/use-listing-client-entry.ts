"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import type { ListingTable } from "@/components/vehicles/ListingTable";
import { useToast } from "@/components/ui/Toast";
import { apiFetch } from "@/lib/api-browser";
import { parsePage, type ApiResult, type Page } from "@/lib/api";
import type { CurrentActor } from "@/lib/auth/actor";
import { parseBranch, type Branch } from "@/lib/branches";
import type { SearchParams } from "@/lib/query";
import { listingParams, listingQuery, type ListingItem } from "@/lib/vehicles/listings";
import { parseListings } from "@/lib/vehicles/parse-listings";
import { parseListingWorkspace, type ListingWorkspace } from "@/lib/vehicles/listing-workspace";

interface ListingEntry extends ReturnType<typeof listingParams> {
  Table: typeof ListingTable;
  initial: ApiResult<Page<ListingItem>>;
  branches: Page<Branch> | null;
  selected: ApiResult<ListingWorkspace> | null;
}

export function useListingClientEntry(actor: CurrentActor, parameters: SearchParams) {
  const [entry, setEntry] = useState<ListingEntry | null>(null);
  const [failed, setFailed] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const { error } = useToast();

  const load = useCallback(async () => {
    if (controller.current) {
      return;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setFailed(false);
    const query = listingParams(parameters);
    const { filters, page, listingId } = query;

    try {
      const [initial, branches, selected, { ListingTable: Table }] = await Promise.all([
        apiFetch(
          `/api/staff/listings?${listingQuery(filters, page)}`,
          parseListings,
          { signal: attempt.signal },
          { requireActor: true },
        ),
        actor.role === "GeneralManager"
          ? apiFetch("/api/intake/branches?page=1", (value) => parsePage(value, parseBranch), {
              signal: attempt.signal,
            }).catch(() => null)
          : null,
        listingId
          ? apiFetch(
              `/api/staff/listings/${listingId}`,
              (value) => {
                const workspace = parseListingWorkspace(value);
                return workspace?.vehicle.vehicleId === listingId ? workspace : null;
              },
              { signal: attempt.signal },
              { requireActor: true },
            ).catch(() => null)
          : null,
        import("@/components/vehicles/ListingTable"),
      ]);
      if (attempt.signal.aborted || controller.current !== attempt) {
        return;
      }
      if (
        initial.data &&
        (initial.data.page !== page || initial.data.pageSize !== filters.pageSize)
      ) {
        throw new Error("Listings unavailable");
      }
      setEntry({
        ...query,
        Table,
        initial,
        branches: branches?.data ?? null,
        selected: listingId
          ? (selected ?? { status: 502, data: null, message: "", uncertain: false })
          : null,
      });
    } catch {
      if (!attempt.signal.aborted && controller.current === attempt) {
        setFailed(true);
        error("Listings could not be loaded. Retry to open the workspace.");
      }
    } finally {
      attempt.abort();
      if (controller.current === attempt) {
        controller.current = null;
      }
    }
  }, [actor, parameters, error]);

  useEffect(() => {
    let active = true;
    queueMicrotask(() => {
      if (active) {
        void load();
      }
    });
    return () => {
      active = false;
      controller.current?.abort();
      controller.current = null;
    };
  }, [load]);

  return { entry, failed, retry: load };
}
