export const REDUCED_MOTION_QUERY = "(prefers-reduced-motion: reduce)";
export const TABBED_LAYOUT_QUERY = "(max-width: 920px)";

// True when the visitor asked the system for less motion; false where there is no window to ask.
export function prefersReducedMotion() {
  return typeof window !== "undefined" && window.matchMedia(REDUCED_MOTION_QUERY).matches;
}

// Brings an element into view, smoothly unless the visitor asked for less motion.
export function scrollIntoViewRespectingMotion(
  element: Element,
  options: Omit<ScrollIntoViewOptions, "behavior"> = { block: "start" },
) {
  element.scrollIntoView({ ...options, behavior: prefersReducedMotion() ? "auto" : "smooth" });
}
