"use client";

import type { AppointmentWorkbench } from "@/components/staff/AppointmentWorkbench";
import { useStaffClientEntry } from "./use-staff-client-entry";
import { privateApiFetch } from "@/lib/api-browser";
import type { ApiResult } from "@/lib/api";
import type { AppointmentDetail } from "@/lib/staff/contracts";
import { parseDetail } from "@/lib/staff/parse";

interface AppointmentEntry {
  Workbench: typeof AppointmentWorkbench;
  initial: ApiResult<AppointmentDetail>;
}

export function useAppointmentClientEntry(id: string) {
  return useStaffClientEntry<AppointmentEntry>(async (signal) => {
    const [initial, { AppointmentWorkbench: Workbench }] = await Promise.all([
      privateApiFetch(`/api/staff/intake/appointments/${id}`, parseDetail, { signal }),
      import("@/components/staff/AppointmentWorkbench"),
    ]);
    signal.throwIfAborted();
    if (initial.status !== 200 || initial.data?.id !== id) {
      throw new Error("Appointment unavailable");
    }
    return { Workbench, initial };
  }, "The appointment could not be loaded. Retry to open it.");
}
