"use client";

import { useEffect, useRef, useState } from "react";
import { prefersReducedMotion } from "@/lib/motion";

const COUNT_DURATION_MS = 350;

// A figure that counts briefly to its new value, and jumps straight to it when the visitor prefers reduced motion.
export function useCountedNumber(target: number) {
  const [shown, setShown] = useState(target);
  const current = useRef(target);

  useEffect(() => {
    const from = current.current;
    const reduced = prefersReducedMotion();
    if (reduced || from === target) {
      current.current = target;
      setShown(target);
      return;
    }
    const started = performance.now();
    let frame = 0;
    function step(now: number) {
      const progress = Math.min(1, (now - started) / COUNT_DURATION_MS);
      const value = from + (target - from) * progress;
      current.current = value;
      setShown(value);
      if (progress < 1) {
        frame = requestAnimationFrame(step);
      }
    }
    frame = requestAnimationFrame(step);
    return () => cancelAnimationFrame(frame);
  }, [target]);

  return shown;
}
