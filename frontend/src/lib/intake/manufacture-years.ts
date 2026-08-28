import type { TaxonomyTrim } from "./contracts";

export function malaysianYear(now: Date) {
  const year = new Intl.DateTimeFormat("en-MY", {
    timeZone: "Asia/Kuala_Lumpur",
    year: "numeric",
  }).format(now);
  return Number(year);
}

// Newest first, from the current Malaysian year back to the trim's first year, matching the range the server accepts.
export function supportedManufactureYears(trim: TaxonomyTrim | undefined, now: Date) {
  if (!trim) {
    return [];
  }
  const newest = malaysianYear(now);
  const length = Math.max(0, newest - trim.firstYear + 1);
  return Array.from({ length }, (_, index) => newest - index);
}
