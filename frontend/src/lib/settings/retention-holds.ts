import { isRecord, isUtcInstant, isUuid } from "@/lib/auth/contracts";

export const holdsPath = "/api/staff/retention-holds";

export interface RetentionHold {
  saleId: string;
  reservationNumber: string;
  until: string;
  reason: string;
}

export interface RetentionHoldList {
  holds: RetentionHold[];
  total: number;
}

export interface RetentionHoldChange {
  until: string | null;
}

const reasonLimit = 1000;

function parseHold(value: unknown): RetentionHold | null {
  if (
    !isRecord(value) ||
    !isUuid(value.saleId) ||
    typeof value.reservationNumber !== "string" ||
    value.reservationNumber.length === 0 ||
    !isUtcInstant(value.until) ||
    typeof value.reason !== "string" ||
    value.reason.length === 0
  ) {
    return null;
  }
  return value as unknown as RetentionHold;
}

// The relay keeps the list inside an object so the signed-in actor can travel beside it.
export function parseRetentionHolds(value: unknown): RetentionHoldList | null {
  const items = isRecord(value) && Array.isArray(value.holds) ? value.holds : null;
  if (!items || items.length > 100) {
    return null;
  }
  const total = isRecord(value) ? value.total : null;
  if (typeof total !== "number" || !Number.isSafeInteger(total) || total < items.length) {
    return null;
  }
  const holds = items.map(parseHold);
  return holds.every((hold) => hold !== null) ? { holds: holds as RetentionHold[], total } : null;
}

export function parseRetentionHoldChange(value: unknown): RetentionHoldChange | null {
  if (!isRecord(value) || !(value.until === null || isUtcInstant(value.until))) {
    return null;
  }
  return { until: value.until };
}

// The upstream list already carries its holds and total in one object; the relay validates it as is.
export function wrapRetentionHolds(value: unknown): RetentionHoldList | null {
  return parseRetentionHolds(value);
}

export function validHoldDraft(draft: {
  reservationNumber: string;
  until: string;
  reason: string;
}) {
  const reason = draft.reason.trim();
  return (
    draft.reservationNumber.trim().length > 0 &&
    /^\d{4}-\d{2}-\d{2}$/.test(draft.until) &&
    reason.length > 0 &&
    reason.length <= reasonLimit
  );
}

// A hold lasts to the end of the chosen day in Malaysia time.
export function holdUntilInstant(day: string) {
  return new Date(`${day}T23:59:59+08:00`).toISOString();
}
