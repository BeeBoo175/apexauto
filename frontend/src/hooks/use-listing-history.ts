"use client";

import { useEffect, useRef } from "react";
import { useStaffHistory } from "./use-staff-history";
import { privateApiFetch } from "@/lib/api-browser";
import type { ListingWorkspace } from "@/lib/vehicles/listing-workspace";
import { appendListingHistory, parseListingHistory } from "@/lib/vehicles/listing-history";

export function useListingHistory(snapshot: ListingWorkspace, active: boolean) {
  const { vehicleId, branchId } = snapshot.vehicle;
  const previous = useRef(snapshot);
  const history = useStaffHistory(
    null,
    {
      path: `/api/staff/listings/${vehicleId}/history`,
      parse: parseListingHistory,
      matches: (value) => value.vehicleId === vehicleId && value.branchId === branchId,
      append: appendListingHistory,
      read: privateApiFetch,
    },
    "Listing history could not be refreshed. Revealed records and your draft remain shown.",
    { enabled: active, progressive: true },
  );

  useEffect(() => {
    const before = previous.current;
    previous.current = snapshot;
    if (before === snapshot || before.vehicle.vehicleId !== vehicleId || !history.attempted) {
      return;
    }
    void history.refresh();
  }, [snapshot, vehicleId, history]);

  return history;
}
