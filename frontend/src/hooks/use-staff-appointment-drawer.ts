"use client";

import { useCallback, useState } from "react";
import { useStaffSelection } from "./use-staff-selection";
import { useGuardedAction } from "./use-guarded-action";
import type { ApiResult } from "@/lib/api";
import { emptyCheckIn, type AppointmentDetail, type CheckInDraft } from "@/lib/staff/contracts";
import { parseDetail } from "@/lib/staff/parse";
import { privateApiFetch } from "@/lib/api-browser";

export function useStaffAppointmentDrawer(
  selectedId: string,
  selected: ApiResult<AppointmentDetail> | null,
  commitAfterRead = false,
  address?: (id: string, updateAddress?: boolean) => void,
) {
  const interaction = useGuardedAction();
  const selection = useStaffSelection(selectedId, selected, {
    parameter: "appointment",
    path: (id) => `/api/staff/intake/appointments/${id}`,
    parse: parseDetail,
    matches: (value, id) => value.id === id,
    read: privateApiFetch,
    commitAfterRead,
    address,
  });
  const [drafts, setDrafts] = useState<Record<string, CheckInDraft>>({});
  const [locked, setLocked] = useState(false);
  const lock = useCallback((value: boolean) => setLocked(value), []);
  return {
    ...selection,
    clearSelection: selection.close,
    locked,
    lock,
    draft: drafts[selection.id] ?? emptyCheckIn,
    setDraft: (draft: CheckInDraft) =>
      setDrafts((current) => ({ ...current, [selection.id]: draft })),
    open: (id: string) => {
      void selection.choose(id);
    },
    close: () => {
      interaction.request(() => selection.close(), "Discard and close");
    },
  };
}
