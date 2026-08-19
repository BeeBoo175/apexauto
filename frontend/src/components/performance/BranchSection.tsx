"use client";

import { EmptyChart } from "./EmptyChart";
import { useState } from "react";
import { usePerformanceAddressContext } from "@/hooks/use-performance-address";
import { Select } from "@/components/ui/Input";
import { formatCurrency } from "@/lib/currency-format";
import { deltaText, weekLabel } from "@/lib/performance/format";
import { branchLabel } from "@/lib/branches";
import { compactMoney } from "@/lib/currency-format";
import type {
  BranchRow,
  PerformanceBranches,
  PeriodTotals,
  StockWeek,
} from "@/lib/performance/performance";
import type { Stat } from "./StatGrid";
import { colors } from "./chart-kit";
import { Legend } from "./Legend";
import { LineChart } from "./LineChart";
import { DashboardSection, type SectionState } from "@/components/staff/DashboardSection";
import { RowsSkeleton } from "@/components/staff/DashboardSkeletons";
import styles from "./Performance.module.css";
import dashboard from "@/components/staff/DashboardSection.module.css";

type MeasureKey = "delivered" | "value" | "checkedIn";

interface BranchMeasure {
  key: MeasureKey;
  label: string;
  // What one figure of this measure counts, for the sentence under the chart and the empty message.
  noun: string;
  week: (week: StockWeek) => number;
  total: (totals: PeriodTotals) => number;
  text: (amount: number) => string;
}

const measures: BranchMeasure[] = [
  {
    key: "delivered",
    label: "Cars delivered",
    noun: "cars delivered",
    week: (week) => week.deliveries,
    total: (totals) => totals.vehiclesDelivered,
    text: (amount) => String(amount),
  },
  {
    key: "value",
    label: "Delivered value",
    noun: "delivered value",
    week: (week) => week.deliveredValue,
    total: (totals) => totals.deliveredValue,
    text: (amount) => formatCurrency(amount),
  },
  {
    key: "checkedIn",
    label: "Cars checked in",
    noun: "cars checked in",
    week: (week) => week.checkIns,
    // Check-ins have no period total on the headline, so the sentence uses the weekly sum.
    total: () => 0,
    text: (amount) => String(amount),
  },
];

// Branch lines keep a fixed colour by branch order, so a branch looks the same on every measure.
const lineColors = [colors.blue, colors.green, colors.amber, colors.violet];

const measureOf = (value: string): MeasureKey =>
  measures.find((item) => item.key === value)?.key ?? "delivered";

const sum = (weeks: StockWeek[], pick: (week: StockWeek) => number) =>
  weeks.reduce((total, week) => total + pick(week), 0);

// The branches together: the headline figures summed over every active branch, each against the period before.
export function branchStats(branches: PerformanceBranches | null): Stat[] {
  const rows = branches?.branches ?? [];
  const add = (pick: (totals: PeriodTotals) => number, side: "current" | "previous") =>
    rows.reduce((sum, row) => sum + pick(row[side]), 0);
  const stat = (
    label: string,
    pick: (totals: PeriodTotals) => number,
    text: (value: number) => string,
    higherIsBetter: boolean,
  ): Stat => ({
    label,
    value: text(add(pick, "current")),
    note: deltaText(add(pick, "current"), add(pick, "previous"), higherIsBetter),
  });
  return [
    stat("Delivered value", (totals) => totals.deliveredValue, formatCurrency, true),
    stat("Vehicles delivered", (totals) => totals.vehiclesDelivered, String, true),
    stat("Vehicles listed", (totals) => totals.listedCount, String, true),
    stat("Customer no-shows", (totals) => totals.customerNoShows, String, false),
  ];
}

// How the branches are doing over time: one line per branch across the weeks of the period, for the measure
// chosen, with a sentence per branch saying where it ended up against the period before. Opening a branch narrows
// Results to it.
export function BranchSection({
  branches,
  state,
  onOpen,
}: {
  branches: PerformanceBranches | null;
  state: SectionState;
  onOpen: (branchId: string) => void;
}) {
  const address = usePerformanceAddressContext();
  const requested = measures.find((item) => item.key === address.parameters.measure);
  const [measureKey, setMeasureKey] = useState<MeasureKey>(requested?.key ?? "delivered");
  function chooseMeasure(next: MeasureKey) {
    setMeasureKey(next);
    address.patch({ measure: next === "delivered" ? "" : next });
  }
  const measure = measures.find((item) => item.key === measureKey) ?? measures[0];
  const rows = branches?.branches ?? [];
  return (
    <DashboardSection
      title="Branches"
      tools={
        <div className={styles.measureSelect}>
          <Select
            label="Measure"
            labelHidden
            density="compact"
            value={measureKey}
            onChange={(event) => chooseMeasure(measureOf(event.target.value))}
          >
            {measures.map((item) => (
              <option key={item.key} value={item.key}>
                {item.label}
              </option>
            ))}
          </Select>
        </div>
      }
      aside={
        <Legend
          label="Branches"
          items={rows.map((row, index) => ({
            key: row.branchId,
            label: branchLabel(row.branchName),
            color: lineColors[index % lineColors.length],
          }))}
        />
      }
      state={{ ...state, hasData: branches !== null }}
      placeholder={<RowsSkeleton count={3} tall />}
    >
      {rows.length === 0 ? (
        <p className={dashboard.note}>No active branches to compare.</p>
      ) : (
        <>
          <BranchChart rows={rows} measure={measure} onOpen={onOpen} />
          <ul className={styles.branchSummaries}>
            {rows.map((row, index) => (
              <BranchSummary
                key={row.branchId}
                row={row}
                measure={measure}
                lineColor={lineColors[index % lineColors.length]}
                onOpen={() => onOpen(row.branchId)}
              />
            ))}
          </ul>
        </>
      )}
    </DashboardSection>
  );
}

function BranchChart({
  rows,
  measure,
  onOpen,
}: {
  rows: BranchRow[];
  measure: BranchMeasure;
  onOpen: (branchId: string) => void;
}) {
  const weeks = rows[0]?.weeks ?? [];
  const maximum = Math.max(
    0,
    ...rows.flatMap((row) => row.weeks.map((week) => measure.week(week))),
  );
  if (weeks.length === 0 || maximum === 0) {
    return (
      <EmptyChart art="line" icon="map-pin" title={`No ${measure.noun}`}>
        Branch figures by week appear here.
      </EmptyChart>
    );
  }
  return (
    <LineChart
      label={`${measure.label} by week for each branch`}
      series={rows.map((row, index) => ({
        key: row.branchId,
        label: branchLabel(row.branchName),
        color: lineColors[index % lineColors.length],
        values: row.weeks.map((week) => measure.week(week)),
        onSelect: () => onOpen(row.branchId),
      }))}
      labels={weeks.map((week) => weekLabel(week.weekStart))}
      formatValue={measure.text}
      formatTick={measure.key === "value" ? compactMoney : String}
      titleFor={(column) => `Week of ${weekLabel(weeks[column].weekStart)}`}
    />
  );
}

function BranchSummary({
  row,
  measure,
  lineColor,
  onOpen,
}: {
  row: BranchRow;
  measure: BranchMeasure;
  lineColor: string;
  onOpen: () => void;
}) {
  const current =
    measure.key === "checkedIn" ? sum(row.weeks, measure.week) : measure.total(row.current);
  const previous = measure.key === "checkedIn" ? null : measure.total(row.previous);
  const change = previous === null ? null : deltaText(current, previous, true);
  return (
    <li>
      <button type="button" className={styles.branchSummary} onClick={onOpen}>
        <span className={styles.swatch} style={{ background: lineColor }} aria-hidden="true" />
        <span className={styles.branchName}>{branchLabel(row.branchName)}</span>
        <span className={styles.branchFigure}>
          {measure.text(current)} {measure.key === "value" ? "" : measure.noun}
        </span>
        {change && <span className={styles.branchChange}>{change.text}</span>}
        <span className="sr-only">Open this branch&apos;s results</span>
      </button>
    </li>
  );
}
