"use client";

import { useStaffClientEntry } from "./use-staff-client-entry";
import { privateApiFetch } from "@/lib/api-browser";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import { readBranchChoices } from "@/lib/branch-choices";
import { performancePeriod, periodQuery, presetOf } from "@/lib/performance/period";
import { parseHeadline } from "@/lib/performance/performance";
import type { SearchParams } from "@/lib/query";

// The entry confirms the viewer and reads the headline, the one section every manager needs at once; the other
// sections load independently from the mounted workspace.
export function usePerformanceEntry(actor: CurrentActor, parameters: SearchParams) {
  return useStaffClientEntry(async (signal) => {
    const period = performancePeriod(parameters);
    const [headline, branches] = await Promise.all([
      privateApiFetch(`/api/staff/performance/headline?${periodQuery(period)}`, parseHeadline, {
        signal,
      }),
      actor.role === "GeneralManager"
        ? readBranchChoices(1, signal, 25).catch(() => null)
        : Promise.resolve(null),
    ]);
    if (!headline.data || actorIdentity(headline.data.actor) !== actorIdentity(actor)) {
      throw new Error("Performance unavailable");
    }
    return {
      actor,
      period,
      preset: presetOf(parameters),
      asTable: parameters.view === "table",
      headline: headline.data,
      branches: branches?.data ?? null,
    };
  }, "Performance could not be loaded. Retry to open the page.");
}
