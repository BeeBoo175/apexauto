import { isUtcInstant, isUuid } from "@/lib/auth/contracts";
import { calendarDate } from "@/lib/calendar";

export const PREFILL_BRANCH_KEY = "branchId";
export const PREFILL_STARTS_AT_KEY = "startsAt";

export interface AppointmentPrefill {
  branchId: string;
  // The chosen bay when the link names one; its day is read in Malaysia time.
  startsAt: string | null;
  date: string | null;
}

// The showroom booking block links to the seller form with the chosen showroom and bay. The address is only a
// request: anything malformed is ignored, and a bay is honoured only when the live slot read still offers it.
export function parseAppointmentPrefill(search: string): AppointmentPrefill | null {
  const params = new URLSearchParams(search);
  const branchId = params.get(PREFILL_BRANCH_KEY);
  if (!isUuid(branchId)) {
    return null;
  }
  const startsAt = params.get(PREFILL_STARTS_AT_KEY);
  if (!isUtcInstant(startsAt)) {
    return { branchId, startsAt: null, date: null };
  }
  return { branchId, startsAt, date: calendarDate(new Date(startsAt)) };
}

export function sellerPrefillHref(branchId: string, startsAt: string) {
  const params = new URLSearchParams({
    [PREFILL_BRANCH_KEY]: branchId,
    [PREFILL_STARTS_AT_KEY]: startsAt,
  });
  return `/sell?${params.toString()}`;
}
