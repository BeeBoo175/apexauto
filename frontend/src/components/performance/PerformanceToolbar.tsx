"use client";

import { useState } from "react";
import { AppliedWorklistFilters } from "@/components/staff/AppliedWorklistFilters";
import { BranchFilter } from "@/components/staff/BranchFilter";
import { Button } from "@/components/ui/Button";
import { ColumnFilterForm } from "@/components/ui/data-table/ColumnFilterForm";
import filterForm from "@/components/ui/data-table/ColumnFilterForm.module.css";
import { FilterChoices } from "@/components/ui/FilterChoices";
import { Input } from "@/components/ui/Input";
import { RecordToolbar } from "@/components/ui/RecordToolbar";
import type { Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import { dateRangeError } from "@/lib/intake/list-query";
import { rangeText } from "@/lib/performance/format";
import { performancePeriod, periodPresets, type PeriodPreset } from "@/lib/performance/period";
import type { PerformancePeriod } from "@/lib/performance/performance";
import type { AppliedFilter } from "@/lib/staff/filter-summary";
import type { TeamExport } from "./TeamView";

interface PerformanceFilters {
  preset: PeriodPreset;
  period: PerformancePeriod;
}

// The default every filter returns to: the last 90 days, every branch the viewer may read.
function defaults(): PerformanceFilters {
  return { preset: "last-90", period: performancePeriod({}) };
}

// The same toolbar as the table pages: the shared Filters popover with a draft that applies or clears together,
// applied filters as removable pills beside it, Reset filters when any apply, and the export on the team views.
export function PerformanceToolbar({
  preset,
  period,
  branches,
  showBranches,
  teamExport,
  busy,
  onApply,
}: {
  preset: PeriodPreset;
  period: PerformancePeriod;
  branches: Page<Branch> | null;
  showBranches: boolean;
  teamExport: TeamExport | "pending" | null;
  busy: boolean;
  onApply: (preset: PeriodPreset, period: PerformancePeriod) => void;
}) {
  const current = { preset, period };
  const applied: AppliedFilter<PerformanceFilters>[] = [];
  if (preset !== "last-90") {
    const label = periodPresets.find((item) => item.value === preset)?.label ?? "Period";
    applied.push({
      key: "dates",
      label:
        preset === "custom"
          ? rangeText(period.from, period.to)
          : `${label}, ${rangeText(period.from, period.to)}`,
      without: { preset: "last-90", period: performancePeriod({ branchId: period.branchId }) },
    });
  }
  if (showBranches && period.branchId) {
    applied.push({
      key: "branch",
      label: branches?.items.find((item) => item.id === period.branchId)?.name ?? "Selected branch",
      without: { preset, period: { ...period, branchId: "" } },
    });
  }

  // The period is always on the bar: a removable pill when it was changed, a fixed one for the default.
  const shown: AppliedFilter<PerformanceFilters>[] =
    preset === "last-90"
      ? [
          {
            key: "dates",
            label: `Last 90 days, ${rangeText(period.from, period.to)}`,
            without: current,
            fixed: true,
          },
          ...applied,
        ]
      : applied;
  return (
    <RecordToolbar
      disabled={busy}
      summaryInline
      filterCount={applied.length}
      filterSummary={
        <>
          <AppliedWorklistFilters
            filters={shown}
            disabled={busy}
            onRemove={(next) => onApply(next.preset, next.period)}
          />
        </>
      }
      filters={
        <PerformanceFilterForm
          key={`${preset}:${period.from}:${period.to}:${period.branchId}`}
          current={current}
          branches={branches}
          showBranches={showBranches}
          busy={busy}
          onApply={onApply}
        />
      }
      actions={
        <>
          {applied.length > 0 && (
            <Button
              size="compact"
              variant="ghost"
              disabled={busy}
              onClick={() => {
                const reset = defaults();
                onApply(reset.preset, reset.period);
              }}
            >
              Reset filters
            </Button>
          )}
          {teamExport && (
            <Button
              variant="secondary"
              size="compact"
              disabled={teamExport === "pending" || teamExport.disabled || teamExport.busy}
              onClick={() => teamExport !== "pending" && teamExport.run()}
            >
              {teamExport !== "pending" && teamExport.busy ? "Preparing CSV…" : "Export full CSV"}
            </Button>
          )}
        </>
      }
    />
  );
}

// The popover's draft, laid out like the table date filters: a quick range, the From and To dates it stands for
// (editing them makes it a custom range) and, for general managers, the branch. Clear and Apply act on all of it.
function PerformanceFilterForm({
  current,
  branches,
  showBranches,
  busy,
  onApply,
}: {
  current: PerformanceFilters;
  branches: Page<Branch> | null;
  showBranches: boolean;
  busy: boolean;
  onApply: (preset: PeriodPreset, period: PerformancePeriod) => void;
}) {
  const [preset, setPreset] = useState(current.preset);
  const [from, setFrom] = useState(current.period.from);
  const [to, setTo] = useState(current.period.to);
  const [branchId, setBranchId] = useState(current.period.branchId);
  const [error, setError] = useState("");

  function choosePreset(next: string) {
    const chosen = next as PeriodPreset;
    setPreset(chosen);
    if (chosen !== "custom") {
      const range = performancePeriod({ period: chosen });
      setFrom(range.from);
      setTo(range.to);
    }
  }

  function chooseDate(edit: () => void) {
    edit();
    setPreset("custom");
  }

  async function apply() {
    if (preset !== "custom") {
      onApply(preset, performancePeriod({ period: preset, branchId }));
      return true;
    }
    const invalid = dateRangeError({ from, to });
    setError(invalid);
    if (invalid) {
      return false;
    }
    onApply("custom", { from, to, branchId });
    return true;
  }

  async function clear() {
    const reset = defaults();
    onApply(reset.preset, reset.period);
    return true;
  }

  return (
    <ColumnFilterForm busy={busy} onApply={apply} onClear={clear}>
      <FilterChoices
        label="Period"
        options={periodPresets.map((item) => ({
          value: item.value,
          label: item.value === "custom" ? "Custom range" : item.label,
        }))}
        value={preset}
        disabled={busy}
        onChange={choosePreset}
      />
      <div className={filterForm.range}>
        <Input
          label="From"
          type="date"
          density="compact"
          value={from}
          disabled={busy}
          onChange={(event) => chooseDate(() => setFrom(event.target.value))}
        />
        <Input
          label="To"
          type="date"
          density="compact"
          value={to}
          disabled={busy}
          onChange={(event) => chooseDate(() => setTo(event.target.value))}
        />
      </div>
      <p className={filterForm.help}>Malaysia time · inclusive dates, up to 366 days</p>
      {error && (
        <p role="alert" className={filterForm.error}>
          {error}
        </p>
      )}
      {showBranches && (
        <BranchFilter
          branches={branches}
          branchId={branchId}
          disabled={busy}
          onChange={setBranchId}
          onPage={() => {}}
          emptyLabel="All branches"
          labelHidden={false}
        />
      )}
    </ColumnFilterForm>
  );
}
