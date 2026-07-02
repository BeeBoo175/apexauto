"use client";

import { useId } from "react";
import { BranchFilter } from "@/components/staff/BranchFilter";
import { Button } from "@/components/ui/Button";
import { FilterShelf } from "@/components/ui/FilterShelf";
import { DateRangeFilter } from "@/components/ui/data-table/ColumnFilterForm";
import type { Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import { periodLabel, periodPresets, type PeriodPreset } from "@/lib/performance/period";
import type { PerformancePeriod } from "@/lib/performance/performance";
import styles from "./Performance.module.css";

export function PeriodControl({
  preset,
  period,
  branches,
  showBranches,
  onPreset,
  onCustom,
  onBranch,
}: {
  preset: PeriodPreset;
  period: PerformancePeriod;
  branches: Page<Branch> | null;
  showBranches: boolean;
  onPreset: (preset: PeriodPreset) => void;
  onCustom: (from: string, to: string) => void;
  onBranch: (branchId: string) => void;
}) {
  const id = useId();
  const shelfId = `performance-period-${id.replace(/[^a-zA-Z0-9-]/g, "")}`;
  const anchor = `--performance-period-${id.replace(/[^a-zA-Z0-9-]/g, "")}`;
  return (
    <div className={styles.toolbar}>
      <div className={styles.presets} role="group" aria-label="Period">
        {periodPresets
          .filter((item) => item.value !== "custom")
          .map((item) => (
            <Button
              key={item.value}
              size="compact"
              variant={preset === item.value ? "secondary" : "ghost"}
              aria-pressed={preset === item.value}
              onClick={() => onPreset(item.value)}
            >
              {item.label}
            </Button>
          ))}
        <Button
          size="compact"
          variant={preset === "custom" ? "secondary" : "ghost"}
          aria-pressed={preset === "custom"}
          popoverTarget={shelfId}
          style={{ anchorName: anchor }}
        >
          Custom
        </Button>
        <FilterShelf id={shelfId} anchor={anchor} title="Custom period" onOpenChange={() => {}}>
          <DateRangeFilter
            from={period.from}
            to={period.to}
            busy={false}
            onApply={async (from, to) => {
              if (!from || !to) {
                return true;
              }
              onCustom(from, to);
              return true;
            }}
          />
        </FilterShelf>
      </div>
      {showBranches && (
        <BranchFilter
          branches={branches}
          branchId={period.branchId}
          disabled={false}
          onChange={onBranch}
          onPage={() => {}}
          emptyLabel="All branches"
        />
      )}
      <span className={styles.periodNote}>{periodLabel(period)}, Malaysia time</span>
    </div>
  );
}
