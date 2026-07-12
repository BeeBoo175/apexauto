import type { SearchParams } from "@/lib/query";
import type { PerformancePeriod } from "./performance";

export const periodPresets = [
  { value: "last-90", label: "Last 90 days" },
  { value: "this-month", label: "This month" },
  { value: "last-month", label: "Last month" },
  { value: "custom", label: "Custom" },
] as const;
export type PeriodPreset = (typeof periodPresets)[number]["value"];

const malaysiaOffsetMs = 8 * 60 * 60 * 1000;
const datePattern = /^\d{4}-\d{2}-\d{2}$/;

// Malaysia has no daylight saving, so a fixed offset gives the local calendar day.
function malaysiaToday(now: number) {
  return new Date(now + malaysiaOffsetMs);
}

function isoDate(date: Date) {
  return date.toISOString().slice(0, 10);
}

function utcDay(year: number, month: number, day: number) {
  return new Date(Date.UTC(year, month, day));
}

export function presetOf(parameters: SearchParams): PeriodPreset {
  const value = typeof parameters.period === "string" ? parameters.period : "";
  const preset = periodPresets.find((item) => item.value === value);
  return preset ? preset.value : "last-90";
}

function text(parameters: SearchParams, key: string) {
  return typeof parameters[key] === "string" ? parameters[key] : "";
}

// The inclusive Malaysian calendar days a preset names, as the backend's yyyy-MM-dd range.
export function performancePeriod(parameters: SearchParams, now = Date.now()): PerformancePeriod {
  const preset = presetOf(parameters);
  const today = malaysiaToday(now);
  const year = today.getUTCFullYear();
  const month = today.getUTCMonth();
  const branchId = text(parameters, "branchId");
  if (preset === "last-month") {
    return {
      from: isoDate(utcDay(year, month - 1, 1)),
      to: isoDate(utcDay(year, month, 0)),
      branchId,
    };
  }
  if (preset === "this-month") {
    return { from: isoDate(utcDay(year, month, 1)), to: isoDate(today), branchId };
  }
  if (preset === "custom") {
    const from = text(parameters, "from");
    const to = text(parameters, "to");
    if (datePattern.test(from) && datePattern.test(to) && from <= to) {
      return { from, to, branchId };
    }
  }
  // Ninety days is the default: a month that has just begun has too few weeks to show a trend.
  return {
    from: isoDate(utcDay(year, month, today.getUTCDate() - 89)),
    to: isoDate(today),
    branchId,
  };
}

export function periodQuery(period: PerformancePeriod) {
  const query = new URLSearchParams({ from: period.from, to: period.to });
  if (period.branchId) {
    query.set("branchId", period.branchId);
  }
  return query.toString();
}
