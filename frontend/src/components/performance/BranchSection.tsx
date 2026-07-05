"use client";

import { formatCurrency } from "@/lib/currency-format";
import { formatDays } from "@/lib/performance/format";
import type { PerformanceBranches, PeriodTotals } from "@/lib/performance/performance";
import { PerformanceSection, type SectionState } from "./PerformanceSection";
import { RowsSkeleton } from "./PerformanceSkeletons";
import styles from "./Performance.module.css";

interface BranchMeasure {
  key: string;
  label: string;
  value: (totals: PeriodTotals) => number | null;
  text: (totals: PeriodTotals) => string;
}

const measures: BranchMeasure[] = [
  {
    key: "value",
    label: "Delivered value",
    value: (totals) => totals.deliveredValue,
    text: (totals) => formatCurrency(totals.deliveredValue),
  },
  {
    key: "vehicles",
    label: "Vehicles delivered",
    value: (totals) => totals.vehiclesDelivered,
    text: (totals) => String(totals.vehiclesDelivered),
  },
  {
    key: "listed",
    label: "Check-in to listed",
    value: (totals) => totals.checkInToListedDays,
    text: (totals) => formatDays(totals.checkInToListedDays),
  },
  {
    key: "noShows",
    label: "Customer no-shows",
    value: (totals) => totals.customerNoShows,
    text: (totals) => String(totals.customerNoShows),
  },
];

// One branch on a dumbbell: an open dot for the period before, a filled dot for this period and the line between,
// so the direction and size of the change read before the figures do.
function Dumbbell({
  label,
  current,
  previous,
  maximum,
  currentText,
  previousText,
  onOpen,
}: {
  label: string;
  current: number | null;
  previous: number | null;
  maximum: number;
  currentText: string;
  previousText: string;
  onOpen: () => void;
}) {
  const position = (value: number) => `${(value / maximum) * 100}%`;
  const low = Math.min(current ?? 0, previous ?? current ?? 0);
  const high = Math.max(current ?? 0, previous ?? current ?? 0);
  return (
    <button
      type="button"
      className={styles.dotRow}
      onClick={onOpen}
      aria-label={`${label}, ${currentText} this period, ${previousText} the period before. Open its results`}
    >
      <span className={styles.barLabel}>{label}</span>
      <span className={styles.dotTrack} aria-hidden="true">
        {current !== null && previous !== null && (
          <span
            className={styles.dumbbellLine}
            style={{ left: position(low), width: `${((high - low) / maximum) * 100}%` }}
          />
        )}
        {previous !== null && (
          <span
            className={`${styles.dot} ${styles.dotPrevious}`}
            style={{ left: position(previous) }}
          />
        )}
        {current !== null && <span className={styles.dot} style={{ left: position(current) }} />}
      </span>
      <span className={styles.dumbbellValue}>
        <span className={current === null ? styles.absent : styles.dotValue}>{currentText}</span>
        <span className={styles.barPreviousText}>{previousText} before</span>
      </span>
    </button>
  );
}

// The general manager's comparison: each measure as its own dumbbell chart across the branches, this period
// against the period before. Opening a branch narrows Results to it.
export function BranchSection({
  branches,
  state,
  onOpen,
}: {
  branches: PerformanceBranches | null;
  state: SectionState;
  onOpen: (branchId: string) => void;
}) {
  const rows = branches?.branches ?? [];
  return (
    <PerformanceSection
      title="Branches side by side"
      aside={
        <ul className={styles.legend} aria-label="Dots">
          <li>
            <span className={`${styles.swatch} ${styles.swatchHigh}`} aria-hidden="true" />
            This period
          </li>
          <li>
            <span className={`${styles.swatch} ${styles.swatchOpen}`} aria-hidden="true" />
            Period before
          </li>
        </ul>
      }
      state={{ ...state, hasData: branches !== null }}
      placeholder={<RowsSkeleton count={3} tall />}
    >
      {rows.length === 0 ? (
        <p className={styles.note}>No active branches to compare.</p>
      ) : (
        <div className={styles.multiples}>
          {measures.map((measure) => {
            const maximum = Math.max(
              1,
              ...rows.flatMap((row) => [
                measure.value(row.current) ?? 0,
                measure.value(row.previous) ?? 0,
              ]),
            );
            return (
              <section key={measure.key} className={styles.multiple} aria-label={measure.label}>
                <div className={styles.multipleHead}>
                  <h3 className={styles.multipleTitle}>{measure.label}</h3>
                </div>
                <div className={styles.dotPlot}>
                  {rows.map((row) => (
                    <Dumbbell
                      key={row.branchId}
                      label={row.branchName}
                      current={measure.value(row.current)}
                      previous={measure.value(row.previous)}
                      maximum={maximum}
                      currentText={measure.text(row.current)}
                      previousText={measure.text(row.previous)}
                      onOpen={() => onOpen(row.branchId)}
                    />
                  ))}
                </div>
              </section>
            );
          })}
        </div>
      )}
    </PerformanceSection>
  );
}
