"use client";

import { useMemo } from "react";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import { privateApiFetch } from "@/lib/api-browser";
import { parseStaffTestDriveDetail } from "@/lib/test-drives/staff";
import type { StaffTestDriveDetail } from "@/lib/test-drives/staff-query";
import { useInitialToast } from "./use-initial-toast";
import { useStaffSelection } from "./use-staff-selection";

export function useStaffTestDriveDetail(
  actor: CurrentActor,
  initialId: string,
  initial: StaffTestDriveDetail | null,
  address: (id: string, updateAddress?: boolean) => void,
) {
  const initialResult = useMemo(
    () => (initial ? { status: 200, data: initial, message: "", uncertain: false } : null),
    [initial],
  );
  const selection = useStaffSelection(initialId, initialResult, {
    parameter: "booking",
    address,
    path: (id) => `/api/staff/test-drives/${id}`,
    parse: parseStaffTestDriveDetail,
    matches: (detail, id) =>
      detail.booking.id === id && actorIdentity(detail.actor) === actorIdentity(actor),
    read: privateApiFetch,
    commitAfterRead: true,
    clearOnDenied: true,
    failureMessage: "The test drive could not be opened. Retry to read its current details.",
  });
  useInitialToast(
    initialId && !initial
      ? "The selected test drive could not be opened. Retry it from the workspace."
      : "",
  );

  return {
    id: selection.id,
    snapshot: selection.result?.data ?? null,
    pendingId: selection.pendingId,
    load: (id = selection.id, updateAddress = true) =>
      id ? selection.load(id, undefined, updateAddress) : Promise.resolve(null),
    close: selection.close,
  };
}
