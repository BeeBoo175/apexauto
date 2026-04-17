"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { privateApiFetch } from "@/lib/api-browser";
import {
  matchesListingRecord,
  parseListingRevisionRead,
  type ListingRevisionRead,
} from "@/lib/vehicles/listing-history";
import type { ListingRecordSelection } from "@/lib/vehicles/listing-history-selection";

export function useListingRevision(
  vehicleId: string,
  branchId: string,
  record: ListingRecordSelection,
) {
  const [result, setResult] = useState<ListingRevisionRead | null>(null);
  const [busy, setBusy] = useState(true);
  const [attempt, setAttempt] = useState(0);
  const controller = useRef<AbortController | null>(null);
  const { error } = useToast();
  const { revisionId, eventId, kind } = record;

  useEffect(() => {
    const cancellation = new AbortController();
    controller.current = cancellation;
    async function load() {
      try {
        const next = await privateApiFetch(
          `/api/staff/listings/${vehicleId}/revisions/${revisionId}`,
          parseListingRevisionRead,
          { signal: cancellation.signal },
        );
        if (cancellation.signal.aborted) {
          return;
        }
        if (
          next.status !== 200 ||
          !next.data ||
          next.data.vehicleId !== vehicleId ||
          next.data.branchId !== branchId ||
          !matchesListingRecord(next.data, { revisionId, eventId, kind })
        ) {
          throw new Error("Listing revision unavailable");
        }
        setResult(next.data);
      } catch {
        if (!cancellation.signal.aborted) {
          error("This saved revision could not be loaded. Your current draft remains unchanged.");
        }
      } finally {
        if (!cancellation.signal.aborted) {
          setBusy(false);
        }
      }
    }
    void load();
    return () => cancellation.abort();
  }, [vehicleId, branchId, revisionId, eventId, kind, attempt, error]);

  function retry() {
    controller.current?.abort();
    setBusy(true);
    setAttempt((current) => current + 1);
  }

  return { result, busy, retry };
}
