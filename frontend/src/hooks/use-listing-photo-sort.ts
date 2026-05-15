"use client";

import { useCallback, useEffect, useRef, useState, type PointerEvent } from "react";
import {
  listingPhotoDropIndex,
  reorderListingPhoto,
  type ListingPhotoSlot,
} from "@/lib/vehicles/listing-photos";

interface PhotoDrag {
  id: string;
  pointerId: number;
  sourceKey: string;
  x: number;
  y: number;
  slots: ListingPhotoSlot[];
  bounds: ListingPhotoSlot;
  capture: HTMLDivElement;
  active: boolean;
  position: number;
}

export function useListingPhotoSort(
  ids: string[],
  disabled: boolean,
  onChange: (ids: string[]) => void,
) {
  const container = useRef<HTMLDivElement | null>(null);
  const grid = useCallback((node: HTMLDivElement | null) => {
    container.current = node;
  }, []);
  const gesture = useRef<PhotoDrag | null>(null);
  const [drag, setDrag] = useState<{ id: string; position: number; sourceKey: string } | null>(
    null,
  );
  const [announcement, setAnnouncement] = useState("");
  const sourceKey = ids.join(":");
  const active = !disabled && drag?.sourceKey === sourceKey ? drag : null;
  const visible = active ? reorderListingPhoto(ids, active.id, active.position) : ids;

  useEffect(
    () => () => {
      const current = gesture.current;
      gesture.current = null;
      if (current?.capture.hasPointerCapture(current.pointerId)) {
        current.capture.releasePointerCapture(current.pointerId);
      }
    },
    [],
  );

  function start(event: PointerEvent<HTMLDivElement>, id: string) {
    const element = container.current;
    if (
      disabled ||
      ids.length < 2 ||
      gesture.current ||
      !element ||
      event.button !== 0 ||
      !event.isPrimary
    ) {
      return;
    }
    event.preventDefault();
    const slots = Array.from(element.querySelectorAll<HTMLElement>("[data-listing-photo-id]")).map(
      (element) => element.getBoundingClientRect(),
    );
    gesture.current = {
      id,
      pointerId: event.pointerId,
      sourceKey,
      x: event.clientX,
      y: event.clientY,
      slots,
      bounds: element.getBoundingClientRect(),
      capture: element,
      active: false,
      position: ids.indexOf(id),
    };
    element.setPointerCapture(event.pointerId);
  }

  function move(event: PointerEvent<HTMLDivElement>) {
    const current = gesture.current;
    if (
      !current ||
      current.pointerId !== event.pointerId ||
      disabled ||
      current.sourceKey !== sourceKey
    ) {
      return;
    }
    if (!current.active && Math.hypot(event.clientX - current.x, event.clientY - current.y) < 6) {
      return;
    }
    current.active = true;
    const { left, right, top, bottom } = current.bounds;
    const inside =
      event.clientX >= left &&
      event.clientX <= right &&
      event.clientY >= top &&
      event.clientY <= bottom;
    current.position = inside
      ? listingPhotoDropIndex(current.slots, event.clientX, event.clientY)
      : -1;
    setDrag({ id: current.id, position: current.position, sourceKey });
  }

  function stop(event: PointerEvent<HTMLDivElement>, commit: boolean) {
    const current = gesture.current;
    if (!current || current.pointerId !== event.pointerId) {
      return;
    }
    gesture.current = null;
    setDrag(null);
    if (current.capture.hasPointerCapture(current.pointerId)) {
      current.capture.releasePointerCapture(current.pointerId);
    }
    if (!commit || !current.active || disabled || current.sourceKey !== sourceKey) {
      return;
    }
    const next = reorderListingPhoto(ids, current.id, current.position);
    if (next !== ids) {
      onChange(next);
      setAnnouncement(`Photo moved to position ${current.position + 1}.`);
    }
  }

  function cancel() {
    const current = gesture.current;
    gesture.current = null;
    setDrag(null);
    if (current?.capture.hasPointerCapture(current.pointerId)) {
      current.capture.releasePointerCapture(current.pointerId);
    }
  }

  return { grid, visible, active, announcement, start, move, stop, cancel };
}
