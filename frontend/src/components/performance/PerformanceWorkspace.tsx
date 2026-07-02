"use client";

import { useState } from "react";
import { StaffCommandBar } from "@/components/staff/StaffPage";
import { Button } from "@/components/ui/Button";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { usePerformanceSection } from "@/hooks/use-performance-section";
import type { usePerformanceEntry } from "@/hooks/use-performance-entry";
import { performancePeriod, type PeriodPreset } from "@/lib/performance/period";
import {
  parseBranches,
  parseHeadline,
  parsePipeline,
  parseRoster,
  parseStageWaiting,
  parseStockFlow,
  parseWorkload,
  type PerformancePeriod,
} from "@/lib/performance/performance";
import { BranchSection } from "./BranchSection";
import { HeadlineSection } from "./HeadlineSection";
import { PeriodControl } from "./PeriodControl";
import { PipelineSection } from "./PipelineSection";
import { RosterSection } from "./RosterSection";
import { StageSection } from "./StageSection";
import { StockFlowSection } from "./StockFlowSection";
import { WorkloadSection } from "./WorkloadSection";
import styles from "./Performance.module.css";

type PerformanceEntry = NonNullable<ReturnType<typeof usePerformanceEntry>["entry"]>;

function addressFor(preset: PeriodPreset, period: PerformancePeriod, asTable: boolean) {
  const query = new URLSearchParams();
  if (preset !== "this-month") {
    query.set("period", preset);
  }
  if (preset === "custom") {
    query.set("from", period.from);
    query.set("to", period.to);
  }
  if (period.branchId) {
    query.set("branchId", period.branchId);
  }
  if (asTable) {
    query.set("view", "table");
  }
  const text = query.toString();
  return `/dashboard/performance${text ? `?${text}` : ""}`;
}

export function PerformanceWorkspace({ entry }: { entry: PerformanceEntry }) {
  const { actor } = entry;
  const [preset, setPreset] = useState<PeriodPreset>(entry.preset);
  const [period, setPeriod] = useState(entry.period);
  const [asTable, setAsTable] = useState(entry.asTable);
  const [refreshes, setRefreshes] = useState(0);
  const isGeneral = actor.role === "GeneralManager";

  const headline = usePerformanceSection(
    "headline",
    parseHeadline,
    period,
    actor,
    refreshes,
    "Headline",
  );
  const pipeline = usePerformanceSection(
    "pipeline",
    parsePipeline,
    period,
    actor,
    refreshes,
    "Sales pipeline",
  );
  const stock = usePerformanceSection(
    "stock-flow",
    parseStockFlow,
    period,
    actor,
    refreshes,
    "Stock in and out",
  );
  const stages = usePerformanceSection(
    "stage-waiting",
    parseStageWaiting,
    period,
    actor,
    refreshes,
    "Stage waiting",
  );
  const workload = usePerformanceSection(
    "workload",
    parseWorkload,
    period,
    actor,
    refreshes,
    "Workload",
  );
  const roster = usePerformanceSection(
    "roster",
    parseRoster,
    period,
    actor,
    refreshes,
    "Rostering",
  );
  const branches = usePerformanceSection(
    "branches",
    parseBranches,
    period,
    actor,
    refreshes,
    "Branch comparison",
    isGeneral,
  );
  const sections = [
    headline,
    pipeline,
    stock,
    stages,
    workload,
    roster,
    isGeneral ? branches : null,
  ];
  const busy = sections.some((section) => section?.busy);

  function choose(nextPreset: PeriodPreset, next: PerformancePeriod, table = asTable) {
    setPreset(nextPreset);
    setPeriod(next);
    window.history.replaceState(null, "", addressFor(nextPreset, next, table));
  }

  function toggleTable() {
    const next = !asTable;
    setAsTable(next);
    window.history.replaceState(null, "", addressFor(preset, period, next));
  }

  const state = (section: ReturnType<typeof usePerformanceSection>) => ({
    hasData: section.data !== null,
    busy: section.busy,
    failed: section.failed,
    retry: section.retry,
  });

  return (
    <>
      <LoadingProgress label="Loading performance" active={busy} />
      <StaffCommandBar
        title="Performance"
        actions={
          <>
            <Button
              size="compact"
              variant={asTable ? "secondary" : "ghost"}
              aria-pressed={asTable}
              onClick={toggleTable}
            >
              View as table
            </Button>
            <RefreshButton
              label="Refresh performance"
              busy={busy}
              disabled={busy}
              onClick={() => setRefreshes((value) => value + 1)}
            />
          </>
        }
      />
      <div className={styles.workspace}>
        <PeriodControl
          preset={preset}
          period={period}
          branches={entry.branches}
          showBranches={isGeneral}
          onPreset={(value) =>
            choose(value, performancePeriod({ period: value, branchId: period.branchId }))
          }
          onCustom={(from, to) => choose("custom", { from, to, branchId: period.branchId })}
          onBranch={(branchId) => choose(preset, { ...period, branchId })}
        />
        <div className={styles.grid}>
          <HeadlineSection
            headline={headline.data ?? entry.headline}
            state={{ ...state(headline), hasData: true }}
          />
          <PipelineSection pipeline={pipeline.data} state={state(pipeline)} asTable={asTable} />
          <StageSection stages={stages.data} state={state(stages)} asTable={asTable} />
          <StockFlowSection flow={stock.data} state={state(stock)} asTable={asTable} />
          <RosterSection roster={roster.data} state={state(roster)} asTable={asTable} />
          <WorkloadSection workload={workload.data} state={state(workload)} asTable={asTable} />
          {isGeneral && <BranchSection branches={branches.data} state={state(branches)} />}
        </div>
      </div>
    </>
  );
}
