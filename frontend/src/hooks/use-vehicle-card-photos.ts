"use client";

import { useLayoutEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { apiFetch } from "@/lib/api-browser";
import type { PublicVehicle } from "@/lib/vehicles/contracts";
import { parsePublicDetail } from "@/lib/vehicles/public-detail";

export function useVehicleCardPhotos(vehicle: PublicVehicle, photoVersion: number) {
  const identity = JSON.stringify([
    vehicle.id,
    vehicle.publishedAt,
    vehicle.coverPhotoId,
    vehicle.photoCount,
    photoVersion,
  ]);
  const [entry, setEntry] = useState(identity);
  const [photoIds, setPhotoIds] = useState<string[] | null>(null);
  const [position, setPosition] = useState(0);
  const [busy, setBusy] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const { error } = useToast();

  if (entry !== identity) {
    setEntry(identity);
    setPhotoIds(null);
    setPosition(0);
    setBusy(false);
  }

  useLayoutEffect(() => {
    return () => {
      controller.current?.abort();
      controller.current = null;
    };
  }, [identity]);

  async function navigate(direction: -1 | 1) {
    if (controller.current || vehicle.photoCount < 2) {
      return;
    }
    const nextPosition = position + direction;
    const count = photoIds?.length ?? vehicle.photoCount;
    if (nextPosition < 0 || nextPosition >= count) {
      return;
    }
    if (photoIds) {
      setPosition(nextPosition);
      return;
    }

    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    try {
      const result = await apiFetch(
        `/api/vehicles/${vehicle.id}`,
        (value) => parsePublicDetail(value, vehicle.id),
        { signal: attempt.signal },
      );
      if (attempt.signal.aborted || controller.current !== attempt) {
        return;
      }
      const detail = result.data;
      if (!detail) {
        throw new Error("Vehicle photos unavailable");
      }
      if (
        detail.listing.publishedAt !== vehicle.publishedAt ||
        detail.listing.coverPhotoId !== vehicle.coverPhotoId ||
        detail.listing.photoCount !== vehicle.photoCount
      ) {
        error("This listing has changed. Refresh the catalogue before browsing its photos.");
        return;
      }
      setPhotoIds(detail.photoIds);
      setPosition(nextPosition);
    } catch {
      if (!attempt.signal.aborted && controller.current === attempt) {
        error("More photos could not be loaded. Your cover photo remains available. Try again.");
      }
    } finally {
      if (controller.current === attempt) {
        controller.current = null;
        setBusy(false);
      }
    }
  }

  return {
    photoId: photoIds?.[position] ?? vehicle.coverPhotoId,
    position,
    count: photoIds?.length ?? vehicle.photoCount,
    busy,
    canPrevious: position > 0,
    canNext: position < (photoIds?.length ?? vehicle.photoCount) - 1,
    navigate,
  };
}
