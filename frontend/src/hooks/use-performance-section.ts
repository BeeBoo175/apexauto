"use client";

import { privateApiFetch } from "@/lib/api-browser";
import { apiReadError } from "@/lib/api";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import { periodQuery } from "@/lib/performance/period";
import type { PerformancePeriod } from "@/lib/performance/performance";
import { useSectionRead } from "./use-section-read";

interface PerformanceSectionOptions<T extends { actor: CurrentActor }> {
  section: string;
  parse: (value: unknown) => T | null;
  period: PerformancePeriod;
  actor: CurrentActor;
  refreshes: number;
  label: string;
  enabled?: boolean;
  // Extra query parameters beyond the period, such as a team page.
  extra?: string;
  initial?: T | null;
}

// A performance section read through the staff performance relay, accepted only for the viewer who asked.
export function usePerformanceSection<T extends { actor: CurrentActor }>({
  section,
  parse,
  period,
  actor,
  refreshes,
  label,
  enabled = true,
  extra = "",
  initial = null,
}: PerformanceSectionOptions<T>) {
  const query = `${periodQuery(period)}${extra ? `&${extra}` : ""}`;
  return useSectionRead({
    key: `${section}?${query}#${actorIdentity(actor)}`,
    refreshes,
    failure: `${label} could not be loaded. Reload the page to try again.`,
    initial,
    enabled,
    read: async (signal) => {
      const result = await privateApiFetch(`/api/staff/performance/${section}?${query}`, parse, {
        signal,
      });
      if (
        result.status !== 200 ||
        !result.data ||
        actorIdentity(result.data.actor) !== actorIdentity(actor)
      ) {
        throw apiReadError(`${label} unavailable`, result);
      }
      return result.data;
    },
  });
}
