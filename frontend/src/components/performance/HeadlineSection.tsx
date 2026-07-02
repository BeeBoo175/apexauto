import { formatCurrency } from "@/lib/currency-format";
import { changeFrom, formatDays } from "@/lib/performance/format";
import type { PerformanceHeadline } from "@/lib/performance/performance";
import { PerformanceSection, type SectionState } from "./PerformanceSection";
import styles from "./Performance.module.css";

export function HeadlineSection({
  headline,
  state,
}: {
  headline: PerformanceHeadline | null;
  state: SectionState;
}) {
  const { current, previous } = headline ?? {};
  return (
    <PerformanceSection title="Headline" shell="tiles" state={state}>
      {current && previous && (
        <div className={styles.tiles}>
          <Tile
            label="Delivered value"
            value={formatCurrency(current.deliveredValue)}
            change={changeFrom(current.deliveredValue, previous.deliveredValue).text}
          />
          <Tile
            label="Vehicles delivered"
            value={String(current.vehiclesDelivered)}
            change={changeFrom(current.vehiclesDelivered, previous.vehiclesDelivered).text}
          />
          <Tile
            label="Check-in to listed"
            value={formatDays(current.checkInToListedDays)}
            change={`${current.listedCount} vehicles listed, ${previous.listedCount} before`}
          />
          <Tile
            label="Customer no-shows"
            value={String(current.customerNoShows)}
            change={changeFrom(current.customerNoShows, previous.customerNoShows).text}
          />
        </div>
      )}
    </PerformanceSection>
  );
}

function Tile({ label, value, change }: { label: string; value: string; change: string }) {
  return (
    <div className={styles.tile}>
      <span className={styles.tileLabel}>{label}</span>
      <span className={styles.tileValue}>{value}</span>
      <span className={styles.tileChange}>{change}</span>
    </div>
  );
}
