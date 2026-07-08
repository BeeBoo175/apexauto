"use client";

import { useEffect } from "react";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { usePerformanceSection } from "@/hooks/use-performance-section";
import type { CurrentActor } from "@/lib/auth/actor";
import { performancePeriod } from "@/lib/performance/period";
import {
  parseBranches,
  parseHeadline,
  parseStageWaiting,
  parseStockFlow,
  type PerformanceBranches,
  type PerformanceHeadline,
  type PerformancePeriod,
  type PerformanceStageWaiting,
  type PerformanceStockFlow,
} from "@/lib/performance/performance";
import { BranchSection, branchStats } from "./BranchSection";
import { HeadlineFigures } from "./HeadlineFigures";
import { StageNowSection } from "./NowSections";
import { StockFlowSection } from "./StockFlowSection";
import { SummarySection } from "./SummarySection";
import { ValueTrendSection } from "./ValueTrendSection";
import styles from "./Performance.module.css";

interface ViewProps {
  period: PerformancePeriod;
  actor: CurrentActor;
  refreshes: number;
}

const stateOf = (section: ReturnType<typeof usePerformanceSection>) => ({
  hasData: section.data !== null,
  busy: section.busy,
  failed: section.failed,
  retry: section.retry,
});

// Results: how the chosen period went, as headline figures and the weekly flow of vehicles in and out.
export function ResultsView({
  period,
  actor,
  refreshes,
  initialHeadline,
  initial,
  onSettled,
}: ViewProps & {
  onSettled: () => void;
  initialHeadline: PerformanceHeadline;
  // What the entry read for this period, so the view opens complete; empty after the period changes.
  initial: {
    stock: PerformanceStockFlow | null;
    stages: PerformanceStageWaiting | null;
  };
}) {
  const headline = usePerformanceSection(
    "headline",
    parseHeadline,
    period,
    actor,
    refreshes,
    "Headline",
  );
  const stock = usePerformanceSection(
    "stock-flow",
    parseStockFlow,
    period,
    actor,
    refreshes,
    "Vehicles in and out",
    true,
    "",
    initial.stock,
  );
  // The now rows ignore the period; the usual wait looks back over the last 90 days.
  const stages = usePerformanceSection(
    "stage-waiting",
    parseStageWaiting,
    performancePeriod({ period: "last-90", branchId: period.branchId }),
    actor,
    refreshes,
    "Vehicles by stage",
    true,
    "",
    initial.stages,
  );
  const sections = [headline, stock, stages];
  const busy = sections.some((section) => section.busy);
  // Ready to be shown once every section has figures or has failed with its retry.
  const settled = sections.every(
    (section) => section.data !== null || (section.failed && !section.busy),
  );
  useEffect(() => {
    if (settled) {
      onSettled();
    }
  }, [settled, onSettled]);
  return (
    <>
      <LoadingProgress label="Loading results" active={busy} />
      <div className={styles.view}>
        <SummarySection headline={headline.data ?? initialHeadline} state={stateOf(headline)} />
        <div className={styles.grid}>
          <StockFlowSection flow={stock.data} state={stateOf(stock)} />
          <ValueTrendSection flow={stock.data} state={stateOf(stock)} />
        </div>
        <StageNowSection stages={stages.data} state={stateOf(stages)} />
      </div>
    </>
  );
}

export function BranchesView({
  period,
  actor,
  refreshes,
  onOpen,
  initial,
  onSettled,
}: ViewProps & {
  onOpen: (branchId: string) => void;
  initial: PerformanceBranches | null;
  onSettled: () => void;
}) {
  const branches = usePerformanceSection(
    "branches",
    parseBranches,
    period,
    actor,
    refreshes,
    "Branch comparison",
    true,
    "",
    initial,
  );
  const settled = branches.data !== null || (branches.failed && !branches.busy);
  useEffect(() => {
    if (settled) {
      onSettled();
    }
  }, [settled, onSettled]);
  return (
    <>
      <LoadingProgress label="Loading branches" active={branches.busy} />
      <div className={styles.view}>
        <HeadlineFigures
          stats={branchStats(branches.data)}
          state={{ ...stateOf(branches), hasData: branches.data !== null }}
        />
        <BranchSection branches={branches.data} state={stateOf(branches)} onOpen={onOpen} />
      </div>
    </>
  );
}
