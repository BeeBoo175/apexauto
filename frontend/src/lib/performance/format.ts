// Elapsed calendar days, never hours worked, shown to one decimal place.
export function formatDays(days: number | null) {
  if (days === null) {
    return "No data";
  }
  return days < 1 ? "Under a day" : `${days.toFixed(1)} days`;
}

type DeltaTone = "success" | "danger" | "neutral";

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

// Short Monday label such as "2 Nov" from a yyyy-MM-dd week start.
export function weekLabel(weekStart: string) {
  const date = new Date(`${weekStart}T00:00:00Z`);
  return new Intl.DateTimeFormat("en-MY", {
    day: "numeric",
    month: "short",
    timeZone: "UTC",
  }).format(date);
}
