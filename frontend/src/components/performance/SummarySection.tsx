import { formatCurrency } from "@/lib/currency-format";
import { deltaText } from "@/lib/performance/format";
import type { PerformanceHeadline } from "@/lib/performance/performance";
import { HeadlineFigures } from "./HeadlineFigures";
import type { SectionState } from "@/components/staff/DashboardSection";
import type { Stat } from "./StatGrid";

// The four figures a manager reads first, for the chosen period, each with its change on the period before.
export function SummarySection({
  headline,
  state,
}: {
  headline: PerformanceHeadline | null;
  state: SectionState;
}) {
  const stats = headline ? statsOf(headline) : [];
  return <HeadlineFigures stats={stats} state={{ ...state, hasData: headline !== null }} />;
}

function listedValue(days: number | null) {
  if (days === null) {
    return "No listings";
  }
  return days < 1 ? "Under a day" : days.toFixed(1);
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
      unit: days === null || days < 1 ? "" : "days",
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
