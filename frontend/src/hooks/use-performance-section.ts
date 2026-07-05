"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { privateApiFetch } from "@/lib/api-browser";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import { periodQuery } from "@/lib/performance/period";
import type { PerformancePeriod } from "@/lib/performance/performance";

// One dashboard section: its own bounded read, concurrent with the others, that keeps its last confirmed content
// when a refresh fails and offers a deliberate retry. A page whose entry already read the section passes that
// result as `initial`, so the section opens with it and reads again only on refresh, retry or a changed query.
export function usePerformanceSection<T extends { actor: CurrentActor }>(
  section: string,
  parse: (value: unknown) => T | null,
  period: PerformancePeriod,
  actor: CurrentActor,
  refreshes: number,
  label: string,
  enabled = true,
  extra = "",
  initial: T | null = null,
) {
  const [data, setData] = useState<T | null>(initial);
  const [busy, setBusy] = useState(enabled && initial === null);
  const skipFirst = useRef(initial !== null);
  const [failed, setFailed] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const toast = useToast();
  const reported = useRef(false);
  const query = `${periodQuery(period)}${extra ? `&${extra}` : ""}`;

  useEffect(() => {
    if (!enabled) {
      return;
    }
    if (skipFirst.current) {
      skipFirst.current = false;
      return;
    }
    const controller = new AbortController();
    let current = true;
    async function load() {
      setBusy(true);
      try {
        const result = await privateApiFetch(`/api/staff/performance/${section}?${query}`, parse, {
          signal: controller.signal,
        });
        if (!current) {
          return;
        }
        if (
          result.status !== 200 ||
          !result.data ||
          actorIdentity(result.data.actor) !== actorIdentity(actor)
        ) {
          throw new Error(`${label} unavailable`);
        }
        setData(result.data);
        setFailed(false);
        reported.current = false;
      } catch {
        if (!current) {
          return;
        }
        setFailed(true);
        if (!reported.current) {
          reported.current = true;
          toast.error(`${label} could not be loaded. Retry that section.`);
        }
      } finally {
        if (current) {
          setBusy(false);
        }
      }
    }
    void load();
    return () => {
      current = false;
      controller.abort();
    };
    // The parser, toast and label are stable for a section; the request depends on the period and refreshes.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [section, query, actor, refreshes, attempt, enabled]);

  const retry = useCallback(() => setAttempt((value) => value + 1), []);
  return { data, busy, failed, retry };
}
