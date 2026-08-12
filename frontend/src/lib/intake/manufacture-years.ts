import type { TaxonomyTrim } from "./contracts";

export function malaysianYear(now: Date) {
  const year = new Intl.DateTimeFormat("en-MY", {
    timeZone: "Asia/Kuala_Lumpur",
    year: "numeric",
  }).format(now);
  return Number(year);
}

// Newest first, from the trim's last year (or the current Malaysian year while it is still sold) back to its
// first year, matching the range the server accepts.
export function supportedManufactureYears(trim: TaxonomyTrim | undefined, now: Date) {
  if (!trim) {
    return [];
  }
  const newest = Math.min(trim.lastYear ?? Infinity, malaysianYear(now));
  const length = Math.max(0, newest - trim.firstYear + 1);
  return Array.from({ length }, (_, index) => newest - index);
}
