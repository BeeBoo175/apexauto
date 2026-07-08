import { formatCurrency } from "@/lib/currency-format";
import { deltaText, rangeText } from "@/lib/performance/format";
import type { PerformanceHeadline, PerformancePeriod } from "@/lib/performance/performance";
import { HeadlineFigures } from "./HeadlineFigures";
import type { SectionState } from "./PerformanceSection";
import type { Stat } from "./StatGrid";

// The four figures a manager reads first, for the chosen period, each with its change on the period before.
export function SummarySection({
  headline,
  period,
  state,
}: {
  headline: PerformanceHeadline | null;
  period: PerformancePeriod;
  state: SectionState;
}) {
  const stats = headline ? statsOf(headline) : [];
  return (
    <HeadlineFigures
      stats={stats}
      covers={`${rangeText(period.from, period.to)}, compared with the period before`}
      state={{ ...state, hasData: headline !== null }}
    />
  );
}

function listedValue(days: number | null) {
  if (days === null) {
    return "No listings";
  }
  return days < 1 ? "Under 1" : days.toFixed(1);
}

function statsOf({ current, previous }: PerformanceHeadline): Stat[] {
  const days = current.checkInToListedDays;
  return [
    {
      label: "Delivered value",
      value: formatCurrency(current.deliveredValue),
      note: deltaText(current.deliveredValue, previous.deliveredValue, true),
    },
    {
      label: "Vehicles delivered",
      value: String(current.vehiclesDelivered),
      note: deltaText(current.vehiclesDelivered, previous.vehiclesDelivered, true),
    },
    {
      label: "Check-in to listed",
      value: listedValue(days),
      unit: days === null ? "" : "days",
      note: {
        text: `${current.listedCount} vehicles listed`,
        tone: "neutral" as const,
      },
    },
    {
      label: "Customer no-shows",
      value: String(current.customerNoShows),
      note: deltaText(current.customerNoShows, previous.customerNoShows, false),
    },
  ];
}
