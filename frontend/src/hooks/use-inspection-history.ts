"use client";

import { useStaffHistory } from "./use-staff-history";
import { inspectionFetch } from "@/lib/inspection/browser";
import { appendInspectionHistory, type InspectionHistoryPage } from "@/lib/inspection/history";

type HistoryOptions = { enabled?: boolean; progressive?: boolean; attempted?: boolean };

export function useInspectionHistory<T extends InspectionHistoryPage>(
  initial: T | null,
  path: string,
  parse: (value: unknown) => T | null,
  failure: string,
  source?: { inspectionId: string; revision?: number },
  options: HistoryOptions = {},
) {
  const inspectionId = initial?.inspectionId ?? source?.inspectionId;
  const revision = initial && "revision" in initial ? initial.revision : source?.revision;
  return useStaffHistory(
    initial,
    {
      path: inspectionId ? path : "",
      parse,
      matches: (value) =>
        value.inspectionId === inspectionId &&
        (revision === undefined || ("revision" in value && value.revision === revision)),
      append: appendInspectionHistory,
      read: inspectionFetch,
    },
    failure,
    { ...options, enabled: options.enabled !== false && Boolean(inspectionId) },
  );
}
