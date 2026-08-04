"use client";

import {
  workshopHref,
  workshopLocation,
  workshopWorkspace,
  type WorkshopState,
} from "@/lib/inspection/workshop-location";
import { useSelectedRecord, useStaffAddress, type AddressMode } from "./use-staff-address";

export type WorkshopTarget = NonNullable<ReturnType<typeof workshopLocation>>;

// The query that identifies the list shown beside the selection, so section-only traversals can be told apart.
export function workshopListKey(state: WorkshopState, technician: boolean) {
  return workshopHref(
    { ...state, id: "", section: "inspection", request: null, category: "" },
    technician,
  );
}

export const useWorkshopSelectedRecord = useSelectedRecord;

// The path selects the record and section, the query holds the list and the checklist category.
export function useWorkshopAddress(
  initial: WorkshopState,
  technician: boolean,
  synchronize: (target: WorkshopTarget, previous: WorkshopTarget | null) => void,
) {
  const address = useStaffAddress<WorkshopState, WorkshopTarget>({
    workspace: workshopWorkspace,
    initial,
    href: (state) => workshopHref(state, technician),
    locate: (pathname, search) => workshopLocation(pathname, search, technician),
    synchronize,
  });
  const apply = (change: Partial<WorkshopState>, mode: AddressMode) => address.update(change, mode);
  return {
    confirmed: address.confirmed,
    select: apply,
    list: apply,
    destination: address.destination,
    attempted: address.attempted,
  };
}
