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
  const controller = useRef<AbortController | null>(null);
  const { error } = useToast();

  if (entry !== identity) {
    setEntry(identity);
    setPhotoIds(null);
    setPosition(0);
  }

  useLayoutEffect(() => {
    return () => {
      controller.current?.abort();
      controller.current = null;
    };
  }, [identity]);

  async function navigate(direction: -1 | 1) {
    if (vehicle.photoCount < 2) {
      return;
    }
    const nextPosition = position + direction;
    const count = photoIds?.length ?? vehicle.photoCount;
    if (nextPosition < 0 || nextPosition >= count) {
      return;
    }
    setPosition(nextPosition);
    if (photoIds || controller.current) {
      return;
    }

    const attempt = new AbortController();
    controller.current = attempt;
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
        error("This listing has changed. Reload this page before browsing its photos.");
        setPosition(0);
        return;
      }
      setPhotoIds(detail.photoIds);
    } catch {
      if (!attempt.signal.aborted && controller.current === attempt) {
        setPosition(0);
        error(
          "More photos could not be loaded. Your cover photo remains available. Reload the page to try again.",
        );
      }
    } finally {
      if (controller.current === attempt) {
        controller.current = null;
      }
    }
  }

  return {
    photoId: position === 0 ? vehicle.coverPhotoId : (photoIds?.[position] ?? null),
    position,
    count: photoIds?.length ?? vehicle.photoCount,
    canPrevious: position > 0,
    canNext: position < (photoIds?.length ?? vehicle.photoCount) - 1,
    navigate,
  };
}
