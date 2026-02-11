import { isCalendarDate } from "./calendar";

export function dateRangeLabel(from: string, to: string) {
  const format = (date: string) =>
    new Intl.DateTimeFormat("en-MY", {
      day: "numeric",
      month: "short",
      year: "numeric",
      timeZone: "UTC",
    }).format(new Date(`${date}T00:00:00Z`));
  if (!from && !to) return "All dates";
  if ((from && !isCalendarDate(from)) || (to && !isCalendarDate(to))) return "Date range";
  if (!from) return `Until ${format(to)}`;
  if (!to) return `From ${format(from)}`;
  if (from === to) return format(from);
  if (from < to) {
    const range = new Intl.DateTimeFormat("en-MY", {
      day: "numeric",
      month: "short",
      year: "numeric",
      timeZone: "UTC",
    }).formatRange(new Date(`${from}T00:00:00Z`), new Date(`${to}T00:00:00Z`));
    // Node and browser ICU versions use different spaces around range separators.
    return range.replace(/[\u00a0\u2009\u202f]/g, " ");
  }
  return `${format(from)} – ${format(to)}`;
}
