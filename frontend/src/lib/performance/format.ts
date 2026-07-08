// Elapsed calendar days, never hours worked, shown to one decimal place.
export function formatDays(days: number | null) {
  if (days === null) {
    return "No data";
  }
  return days < 1 ? "Under a day" : `${days.toFixed(1)} days`;
}

export type DeltaTone = "success" | "danger" | "neutral";

export interface Delta {
  text: string;
  tone: DeltaTone;
}

// The change on the period before, in words. Whether a rise is good depends on the measure.
export function deltaText(current: number, previous: number, higherIsBetter: boolean): Delta {
  if (previous === 0) {
    return current === 0
      ? { text: "None last period either", tone: "neutral" }
      : { text: "Up from none last period", tone: higherIsBetter ? "success" : "danger" };
  }
  const percent = Math.round(((current - previous) / previous) * 100);
  if (percent === 0) {
    return { text: "Same as last period", tone: "neutral" };
  }
  const better = percent > 0 === higherIsBetter;
  return {
    text: `${percent > 0 ? "Up" : "Down"} ${Math.abs(percent)}% on last period`,
    tone: better ? "success" : "danger",
  };
}

export const weekdayLabels = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

export function hourLabel(hour: number) {
  const suffix = hour < 12 ? "am" : "pm";
  const display = hour % 12 === 0 ? 12 : hour % 12;
  return `${display}${suffix}`;
}

// Short Monday label such as "2 Nov" from a yyyy-MM-dd week start.
export function weekLabel(weekStart: string) {
  const date = new Date(`${weekStart}T00:00:00Z`);
  return new Intl.DateTimeFormat("en-MY", {
    day: "numeric",
    month: "short",
    timeZone: "UTC",
  }).format(date);
}

// A short Malaysian-style date range such as "1 Nov to 7 Nov 2026".
export function rangeText(from: string, to: string) {
  const format = (value: string, year: boolean) =>
    new Intl.DateTimeFormat("en-MY", {
      day: "numeric",
      month: "short",
      ...(year ? { year: "numeric" } : {}),
      timeZone: "UTC",
    }).format(new Date(`${value}T00:00:00Z`));
  return `${format(from, false)} to ${format(to, true)}`;
}

// Every branch is named "ApexAuto Bayan Lepas" and so on; on a page that is all ApexAuto the shared first word only
// repeats, so labels show the place.
export function branchLabel(name: string) {
  return name.replace(/^ApexAuto\s+/, "");
}

const compactCurrency = new Intl.NumberFormat("en-MY", {
  style: "currency",
  currency: "MYR",
  notation: "compact",
  maximumFractionDigits: 1,
});

// Short MYR amounts for chart scales, such as "MYR 1.2M"; hovers and tables carry the exact amount.
export function compactMoney(amount: number) {
  return compactCurrency.format(amount);
}
