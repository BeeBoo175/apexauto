"use client";

import { useState } from "react";
import { WorkloadSection } from "@/components/performance/WorkloadSection";
import type { HomeEntry } from "@/hooks/use-home-entry";
import { usePerformanceSection } from "@/hooks/use-performance-section";
import { useTodaySchedule } from "@/hooks/use-today-schedule";
import { performancePeriod } from "@/lib/performance/period";
import { useStaffAddress } from "@/hooks/use-staff-address";
import { homeHref, homeLocation, homeWorkspace, type HomeState } from "@/lib/home/home-location";
import { workloadQuery, type RoomView } from "@/lib/performance/room";
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

// A manager's Home: what needs them now, today's schedule and who has room. Period figures live on Performance and
// the month on the Work calendar.
export function HomeWorkspace({ entry }: { entry: Extract<HomeEntry, { kind: "manager" }> }) {
  const { actor } = entry;
  const general = actor.role === "GeneralManager";
  const [branchId, setBranchId] = useState(entry.branchId);
  const [refreshes, setRefreshes] = useState(0);
  const [room, setRoom] = useState<RoomView>(entry.room);
  // Branch, team, search and page ride in the address: an applied change pushes, and Back or Forward restores it.
  type Target = NonNullable<ReturnType<typeof homeLocation>>;
  const address = useStaffAddress<HomeState, Target>({
    workspace: homeWorkspace,
    initial: { branchId: entry.branchId, room: entry.room },
    href: homeHref,
    locate: (pathname, search) => homeLocation(pathname, search, general),
    synchronize: (target) => {
      setBranchId(target.branchId);
      setRoom(target.room);
      address.update({ branchId: target.branchId, room: target.room }, false);
    },
  });
  function changeRoom(next: RoomView) {
    setRoom(next);
    address.update({ room: next }, "push");
  }
  const now = performancePeriod({ branchId });
  // Every section opens with the entry's read; a branch change or refresh reads again and keeps the figures shown
  // until the new ones arrive.
  const pipeline = usePerformanceSection({
    section: "pipeline",
    parse: parsePipeline,
    period: now,
    actor,
    refreshes,
    label: "Sales",
    initial: entry.pipeline,
  });
  const stages = usePerformanceSection({
    section: "stage-waiting",
    parse: parseStageWaiting,
    period: performancePeriod({ period: "last-90", branchId }),
    actor,
    refreshes,
    label: "Stock",
    initial: entry.stages,
  });
  const workload = usePerformanceSection({
    section: "workload",
    parse: parseWorkload,
    period: now,
    actor,
    refreshes,
    label: "Who has room",
    extra: workloadQuery(room),
    initial: entry.workload,
  });
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
    address.update({ branchId: next }, "push");
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
          view={room}
          onView={changeRoom}
        />
      }
    />
  );
}
