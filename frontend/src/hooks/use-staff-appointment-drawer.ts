"use client";

import { useCallback, useState } from "react";
import { useStaffSelection } from "./use-staff-selection";
import type { ApiResult } from "@/lib/api";
import { emptyCheckIn, type AppointmentDetail, type CheckInDraft } from "@/lib/staff/contracts";
import { parseDetail } from "@/lib/staff/parse";

export function useStaffAppointmentDrawer(
  selectedId: string,
  selected: ApiResult<AppointmentDetail> | null,
) {
  const selection = useStaffSelection(selectedId, selected, {
    parameter: "appointment",
    path: (id) => `/api/staff/intake/appointments/${id}`,
    parse: parseDetail,
    matches: (value, id) => value.id === id,
  });
  const [drafts, setDrafts] = useState<Record<string, CheckInDraft>>({});
  const [locked, setLocked] = useState(false);
  const lock = useCallback((value: boolean) => setLocked(value), []);
  return {
    ...selection,
    locked,
    lock,
    draft: drafts[selection.id] ?? emptyCheckIn,
    setDraft: (draft: CheckInDraft) =>
      setDrafts((current) => ({ ...current, [selection.id]: draft })),
    open: (id: string) => {
      if (!locked) {
        void selection.choose(id);
      }
    },
  };
}
