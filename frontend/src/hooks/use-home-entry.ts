"use client";

import { useStaffClientEntry } from "./use-staff-client-entry";
import { readAgentWork } from "./use-agent-home";
import { readTodaySchedule } from "./use-today-schedule";
import { privateApiFetch } from "@/lib/api-browser";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import { readBranchChoices } from "@/lib/branch-choices";
import { isUuid } from "@/lib/auth/contracts";
import { performancePeriod, periodQuery } from "@/lib/performance/period";
import {
  parsePipeline,
  parseStageWaiting,
  parseWorkload,
  type PerformancePeriod,
  type PerformancePipeline,
  type PerformanceStageWaiting,
  type PerformanceWorkload,
} from "@/lib/performance/performance";
import { parseRoomUrl, workloadQuery, type RoomView } from "@/lib/performance/room";
import type { SearchParams } from "@/lib/query";
import type { SalesQueue } from "@/lib/sales/contracts";
import type { ScheduledWork, UnscheduledWorkPage } from "@/lib/staff/work-calendar";

export type HomeEntry =
  | {
      kind: "manager";
      actor: CurrentActor;
      branchId: string;
      room: RoomView;
      pipeline: PerformancePipeline;
      stages: PerformanceStageWaiting;
      workload: PerformanceWorkload;
      schedule: ScheduledWork[];
      branches: Awaited<ReturnType<typeof readBranchChoices>>["data"] | null;
    }
  | {
      kind: "agent";
      actor: CurrentActor;
      queue: SalesQueue;
      work: UnscheduledWorkPage;
      schedule: ScheduledWork[];
    };

async function readSection<T extends { actor: CurrentActor }>(
  section: string,
  parse: (value: unknown) => T | null,
  period: PerformancePeriod,
  actor: CurrentActor,
  signal: AbortSignal,
  extra = "",
) {
  const result = await privateApiFetch(
    `/api/staff/performance/${section}?${periodQuery(period)}${extra ? `&${extra}` : ""}`,
    parse,
    {
      signal,
    },
  );
  if (!result.data || actorIdentity(result.data.actor) !== actorIdentity(actor)) {
    throw new Error("Home unavailable");
  }
  return result.data;
}

// Home reveals once, like the table pages: the entry reads everything the first screen shows, concurrently, and the
// page replaces its silhouette only when all of it has answered. Later refreshes keep the confirmed figures.
export function useHomeEntry(actor: CurrentActor, parameters: SearchParams) {
  return useStaffClientEntry<HomeEntry>(async (signal) => {
    if (actor.role === "SalesAgent") {
      const [mine, schedule] = await Promise.all([
        readAgentWork(actor, signal),
        readTodaySchedule(actor, "", signal),
      ]);
      return { kind: "agent", actor, ...mine, schedule };
    }
    const general = actor.role === "GeneralManager";
    const branchId = general && isUuid(parameters.branchId) ? parameters.branchId : "";
    const now = performancePeriod({ branchId });
    const room = parseRoomUrl(parameters);
    const [pipeline, stages, workload, schedule, branches] = await Promise.all([
      readSection("pipeline", parsePipeline, now, actor, signal),
      readSection(
        "stage-waiting",
        parseStageWaiting,
        performancePeriod({ period: "last-90", branchId }),
        actor,
        signal,
      ),
      readSection("workload", parseWorkload, now, actor, signal, workloadQuery(room)),
      readTodaySchedule(actor, branchId, signal),
      general ? readBranchChoices(1, signal, 25).catch(() => null) : Promise.resolve(null),
    ]);
    return {
      kind: "manager",
      actor,
      branchId,
      room,
      pipeline,
      stages,
      workload,
      schedule,
      branches: branches?.data ?? null,
    };
  }, "Home could not be loaded. Retry to open the page.");
}
