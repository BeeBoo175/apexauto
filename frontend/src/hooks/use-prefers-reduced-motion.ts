"use client";

import { useMediaQuery } from "@/hooks/use-media-query";
import { REDUCED_MOTION_QUERY } from "@/lib/motion";

// Whether the visitor asked for less motion. The server renders as if they did not, and the browser corrects it as
// soon as the page is running.
export function usePrefersReducedMotion() {
  return useMediaQuery(REDUCED_MOTION_QUERY);
}
