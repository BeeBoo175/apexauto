"use client";

import { useCallback, useState } from "react";
import { useStaffSelection } from "./use-staff-selection";
import type { ApiResult } from "@/lib/intake/contracts";
import { emptyCheckIn, type AppointmentDetail, type CheckInDraft } from "@/lib/staff/contracts";

export function useStaffAppointmentDrawer(
  selectedId: string,
  selected: ApiResult<AppointmentDetail> | null,
) {
  const selection = useStaffSelection(selectedId, selected);
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
