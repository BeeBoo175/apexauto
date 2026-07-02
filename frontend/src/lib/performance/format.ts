// Elapsed calendar days, never hours worked, shown to one decimal place.
export function formatDays(days: number | null) {
  return days === null ? "No data" : `${days.toFixed(1)} days`;
}

export interface Change {
  text: string;
  direction: "up" | "down" | "flat" | "none";
}

// Compares a value with the same measure over the preceding equal-length period; no base means no percentage.
export function changeFrom(current: number, previous: number): Change {
  if (previous === 0) {
    return current === 0
      ? { text: "No change from the previous period", direction: "flat" }
      : { text: "No earlier figure to compare", direction: "none" };
  }
  const percent = Math.round(((current - previous) / previous) * 100);
  if (percent === 0) {
    return { text: "No change from the previous period", direction: "flat" };
  }
  const word = percent > 0 ? "up" : "down";
  return {
    text: `${word} ${Math.abs(percent)}% on the previous period`,
    direction: percent > 0 ? "up" : "down",
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
