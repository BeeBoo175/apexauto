import { formatCurrency } from "@/lib/currency-format";
import { deltaText, rangeText, type Delta } from "@/lib/performance/format";
import type { PerformanceHeadline, PerformancePeriod } from "@/lib/performance/performance";
import { PerformanceSection, type SectionState } from "./PerformanceSection";
import { StatsSkeleton } from "./PerformanceSkeletons";
import styles from "./Performance.module.css";

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
    <PerformanceSection
      title="Headline figures"
      aside={
        <span className={styles.panelAside}>
          {rangeText(period.from, period.to)}, compared with the period before
        </span>
      }
      state={{ ...state, hasData: headline !== null }}
      placeholder={<StatsSkeleton />}
    >
      <dl className={styles.stats}>
        {stats.map((stat) => (
          <div key={stat.label} className={styles.stat}>
            <dt className={styles.statLabel}>{stat.label}</dt>
            <dd className={styles.statValue}>
              {stat.value}
              {stat.unit && <span className={styles.statUnit}> {stat.unit}</span>}
            </dd>
            <dd className={deltaClass(stat.delta)}>{stat.delta.text}</dd>
          </div>
        ))}
      </dl>
    </PerformanceSection>
  );
}

function listedValue(days: number | null) {
  if (days === null) {
    return "No listings";
  }
  return days < 1 ? "Under 1" : days.toFixed(1);
}

function deltaClass(delta: Delta) {
  if (delta.tone === "success") {
    return `${styles.delta} ${styles.deltaUp}`;
  }
  return delta.tone === "danger" ? `${styles.delta} ${styles.deltaDown}` : styles.delta;
}

function statsOf({ current, previous }: PerformanceHeadline) {
  const days = current.checkInToListedDays;
  return [
    {
      label: "Delivered value",
      value: formatCurrency(current.deliveredValue),
      unit: "",
      delta: deltaText(current.deliveredValue, previous.deliveredValue, true),
    },
    {
      label: "Vehicles delivered",
      value: String(current.vehiclesDelivered),
      unit: "",
      delta: deltaText(current.vehiclesDelivered, previous.vehiclesDelivered, true),
    },
    {
      label: "Check-in to listed",
      value: listedValue(days),
      unit: days === null ? "" : "days",
      delta: {
        text: `${current.listedCount} vehicles listed`,
        tone: "neutral" as const,
      },
    },
    {
      label: "Customer no-shows",
      value: String(current.customerNoShows),
      unit: "",
      delta: deltaText(current.customerNoShows, previous.customerNoShows, false),
    },
  ];
}
