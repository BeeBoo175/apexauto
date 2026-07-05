"use client";

import { useStaffClientEntry } from "./use-staff-client-entry";
import { privateApiFetch } from "@/lib/api-browser";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import { readBranchChoices } from "@/lib/branch-choices";
import { performancePeriod, periodQuery, presetOf } from "@/lib/performance/period";
import { parseSalesTeam, parseWorkshopTeam } from "@/lib/performance/team";
import {
  parseBranches,
  parseHeadline,
  parsePipeline,
  parseStageWaiting,
  parseStockFlow,
  type PerformancePeriod,
} from "@/lib/performance/performance";
import type { SearchParams } from "@/lib/query";

// A Results section the entry reads ahead. A failed one comes back empty, so its section offers its own retry while
// the rest of the page still opens.
async function readAhead<T extends { actor: CurrentActor }>(
  section: string,
  parse: (value: unknown) => T | null,
  period: PerformancePeriod,
  actor: CurrentActor,
  signal: AbortSignal,
  extra = "",
) {
  const result = await privateApiFetch(
    `/api/staff/performance/${section}?${periodQuery(period)}${extra}`,
    parse,
    { signal },
  ).catch(() => null);
  const data = result?.data ?? null;
  return data && actorIdentity(data.actor) === actorIdentity(actor) ? data : null;
}

// The first page of a team table, when the address opens on a team tab.
function readTeam(
  view: string,
  period: PerformancePeriod,
  actor: CurrentActor,
  signal: AbortSignal,
) {
  const firstPage = "&page=1&pageSize=25";
  if (view === "sales") {
    return readAhead("team/sales", parseSalesTeam, period, actor, signal, firstPage);
  }
  if (view === "workshop") {
    return readAhead("team/workshop", parseWorkshopTeam, period, actor, signal, firstPage);
  }
  return null;
}

// Performance reveals once, like the table pages: the entry reads every section of the requested view
// concurrently, so the page replaces its silhouette only when that view is complete.
export function usePerformanceEntry(actor: CurrentActor, parameters: SearchParams) {
  return useStaffClientEntry(async (signal) => {
    const period = performancePeriod(parameters);
    const view = typeof parameters.view === "string" ? parameters.view : "results";
    const results = view === "results";
    const lookBack = performancePeriod({ period: "last-90", branchId: period.branchId });
    const [headline, branches, stock, stages, pipeline, team, comparison] = await Promise.all([
      privateApiFetch(`/api/staff/performance/headline?${periodQuery(period)}`, parseHeadline, {
        signal,
      }),
      actor.role === "GeneralManager"
        ? readBranchChoices(1, signal, 25).catch(() => null)
        : Promise.resolve(null),
      results ? readAhead("stock-flow", parseStockFlow, period, actor, signal) : null,
      results ? readAhead("stage-waiting", parseStageWaiting, lookBack, actor, signal) : null,
      results ? readAhead("pipeline", parsePipeline, period, actor, signal) : null,
      readTeam(view, period, actor, signal),
      view === "branches" && actor.role === "GeneralManager"
        ? readAhead("branches", parseBranches, period, actor, signal)
        : null,
    ]);
    if (!headline.data || actorIdentity(headline.data.actor) !== actorIdentity(actor)) {
      throw new Error("Performance unavailable");
    }
    return {
      actor,
      period,
      preset: presetOf(parameters),
      headline: headline.data,
      results: { stock, stages, pipeline },
      team,
      comparison,
      branches: branches?.data ?? null,
    };
  }, "Performance could not be loaded. Retry to open the page.");
}
