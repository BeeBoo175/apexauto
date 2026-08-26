"use client";

import { prefersReducedMotion } from "@/lib/motion";
import { useEffect, useRef, useState } from "react";
import type { MapViewPreset } from "@/lib/guide/malaysia-map";

const PAN_DURATION_MS = 420;

export function useAnimatedViewBox(target: MapViewPreset): MapViewPreset {
  const { x: targetX, y: targetY, width: targetWidth, height: targetHeight } = target;
  const [rendered, setRendered] = useState<MapViewPreset>(() => ({
    x: targetX,
    y: targetY,
    width: targetWidth,
    height: targetHeight,
  }));
  const liveRef = useRef<MapViewPreset>({
    x: targetX,
    y: targetY,
    width: targetWidth,
    height: targetHeight,
  });

  useEffect(() => {
    const nextTarget: MapViewPreset = {
      x: targetX,
      y: targetY,
      width: targetWidth,
      height: targetHeight,
    };
    const from = liveRef.current;
    const isUnchanged =
      Math.abs(from.x - targetX) < 0.05 &&
      Math.abs(from.y - targetY) < 0.05 &&
      Math.abs(from.width - targetWidth) < 0.05 &&
      Math.abs(from.height - targetHeight) < 0.05;

    if (isUnchanged) {
      return;
    }

    if (prefersReducedMotion()) {
      liveRef.current = nextTarget;
      const immediate = requestAnimationFrame(() => setRendered(nextTarget));
      return () => cancelAnimationFrame(immediate);
    }

    let rafId = 0;
    const startTime = performance.now();

    function step(now: number) {
      const elapsed = Math.min(1, (now - startTime) / PAN_DURATION_MS);
      const eased = 1 - Math.pow(1 - elapsed, 3);
      const next: MapViewPreset = {
        x: from.x + (targetX - from.x) * eased,
        y: from.y + (targetY - from.y) * eased,
        width: from.width + (targetWidth - from.width) * eased,
        height: from.height + (targetHeight - from.height) * eased,
      };
      liveRef.current = next;
      setRendered(next);

      if (elapsed < 1) {
        rafId = requestAnimationFrame(step);
      }
    }

    rafId = requestAnimationFrame(step);
    return () => cancelAnimationFrame(rafId);
  }, [targetX, targetY, targetWidth, targetHeight]);

  return rendered;
}
