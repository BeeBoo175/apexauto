"use client";

import { useState } from "react";
import { WorkloadSection } from "@/components/performance/WorkloadSection";
import type { HomeEntry } from "@/hooks/use-home-entry";
import { usePerformanceSection } from "@/hooks/use-performance-section";
import { useTodaySchedule } from "@/hooks/use-today-schedule";
import { performancePeriod } from "@/lib/performance/period";
import { parsePipeline, parseStageWaiting, parseWorkload } from "@/lib/performance/performance";
import { AttentionBand, managerAttention } from "./AttentionSection";
import { HomeBranchToolbar } from "./HomeBranchToolbar";
import { HomeLayout } from "./HomeLayout";
import { ScheduleSection } from "./ScheduleSection";

const stateOf = (section: {
  data: unknown;
  busy: boolean;
  failed: boolean;
  retry: () => void;
}) => ({
  hasData: section.data !== null,
  busy: section.busy,
  failed: section.failed,
  retry: section.retry,
});

// The branch rides in the address, so a reload returns to the same scope.
function writeBranch(branchId: string) {
  const url = new URL(window.location.href);
  if (branchId) {
    url.searchParams.set("branchId", branchId);
  } else {
    url.searchParams.delete("branchId");
  }
  window.history.replaceState(null, "", url);
}

// A manager's Home: what needs them now, today's schedule and who has room. Period figures live on Performance and
// the month on the Work calendar.
export function HomeWorkspace({ entry }: { entry: Extract<HomeEntry, { kind: "manager" }> }) {
  const { actor } = entry;
  const general = actor.role === "GeneralManager";
  const [branchId, setBranchId] = useState(entry.branchId);
  const [refreshes, setRefreshes] = useState(0);
  const now = performancePeriod({ branchId });
  // Every section opens with the entry's read; a branch change or refresh reads again and keeps the figures shown
  // until the new ones arrive.
  const pipeline = usePerformanceSection(
    "pipeline",
    parsePipeline,
    now,
    actor,
    refreshes,
    "Sales",
    true,
    "",
    entry.pipeline,
  );
  const stages = usePerformanceSection(
    "stage-waiting",
    parseStageWaiting,
    performancePeriod({ period: "last-90", branchId }),
    actor,
    refreshes,
    "Stock",
    true,
    "",
    entry.stages,
  );
  const workload = usePerformanceSection(
    "workload",
    parseWorkload,
    now,
    actor,
    refreshes,
    "Who has room",
    true,
    "",
    entry.workload,
  );
  const schedule = useTodaySchedule(actor, branchId, refreshes, entry.schedule);
  const sections = [pipeline, stages, workload, schedule];
  const sources = {
    pipeline: pipeline.data,
    stages: stages.data,
    workload: workload.data,
    schedule: schedule.data,
  };
  const ready = Object.values(sources).every((source) => source !== null);

  function chooseBranch(next: string) {
    setBranchId(next);
    writeBranch(next);
  }

  return (
    <HomeLayout
      busy={sections.some((section) => section.busy)}
      toolbar={
        general ? (
          <HomeBranchToolbar
            branches={entry.branches}
            branchId={branchId}
            busy={sections.some((section) => section.busy)}
            onBranch={chooseBranch}
          />
        ) : null
      }
      onRefresh={() => setRefreshes((value) => value + 1)}
      band={
        <AttentionBand
          groups={managerAttention(sources)}
          ready={ready}
          failed={sections.some((section) => section.failed)}
        />
      }
      left={
        <ScheduleSection
          items={schedule.data}
          state={stateOf(schedule)}
          showBranch={general && !branchId}
        />
      }
      right={
        <WorkloadSection
          workload={workload.data}
          state={stateOf(workload)}
          showBranch={general && !branchId}
        />
      }
    />
  );
}
