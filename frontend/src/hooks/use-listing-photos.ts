"use client";

import { useCallback, useLayoutEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import type { Page } from "@/lib/api";
import { privateApiFetch } from "@/lib/api-browser";
import { parseListingPhotos, type ListingPhotoItem } from "@/lib/vehicles/listing-photos";
import type { ListingWorkspace } from "@/lib/vehicles/listing-workspace";

export function useListingPhotos(workspace: ListingWorkspace) {
  const [snapshot, setSnapshot] = useState<Page<ListingPhotoItem> | null>(null);
  const [busy, setBusy] = useState(false);
  const [failed, setFailed] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const attempted = useRef(false);
  const currentPage = useRef(1);
  const retryPage = useRef(1);
  const { error } = useToast();
  const vehicleId = workspace.vehicle.vehicleId;

  const load = useCallback(
    async (page = 1) => {
      if (controller.current) {
        return;
      }
      const attempt = new AbortController();
      controller.current = attempt;
      attempted.current = true;
      retryPage.current = page;
      setBusy(true);
      setFailed(false);
      try {
        const result = await privateApiFetch(
          `/api/staff/listings/${vehicleId}/photos?page=${page}`,
          parseListingPhotos,
          { signal: attempt.signal },
        );
        if (attempt.signal.aborted || controller.current !== attempt) {
          return;
        }
        if (
          result.status !== 200 ||
          !result.data ||
          result.data.page !== page ||
          result.data.pageSize !== 10
        ) {
          throw new Error("Photo choices unavailable");
        }
        setSnapshot(result.data);
        currentPage.current = page;
      } catch {
        if (!attempt.signal.aborted && controller.current === attempt) {
          setFailed(true);
          error(
            "Uploaded photos could not be refreshed. Your selected photos and last confirmed choices remain.",
          );
        }
      } finally {
        if (controller.current === attempt) {
          controller.current = null;
          setBusy(false);
        }
      }
    },
    [vehicleId, error],
  );

  useLayoutEffect(() => {
    let active = true;
    queueMicrotask(() => {
      if (active && attempted.current) {
        void load(currentPage.current);
      }
    });
    return () => {
      active = false;
      controller.current?.abort();
      controller.current = null;
    };
  }, [workspace, load]);

  return { snapshot, busy, failed, load, retry: () => load(retryPage.current) };
}
