import { RESERVATION_DEPOSIT } from "./deposit-terms";
import { isRecord, isUtcInstant, isUuid } from "@/lib/auth/contracts";

export interface CustomerReservationCancellation {
  id: string;
  reservationId: string;
  kind: "CustomerCancelled";
  actorId: string;
  reason: null;
  recordedAt: string;
  refund: number;
}

export function parseCustomerCancellation(value: unknown): CustomerReservationCancellation | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !isUuid(value.reservationId) ||
    value.kind !== "CustomerCancelled" ||
    !isUuid(value.actorId) ||
    value.reason !== null ||
    !isUtcInstant(value.recordedAt) ||
    value.refund !== RESERVATION_DEPOSIT
  ) {
    return null;
  }
  return {
    id: value.id,
    reservationId: value.reservationId,
    kind: value.kind,
    actorId: value.actorId,
    reason: null,
    recordedAt: value.recordedAt,
    refund: value.refund,
  };
}

export function matchesCustomerCancellation(
  receipt: CustomerReservationCancellation,
  body: unknown,
  reservationId: string,
  actorId: string,
) {
  return (
    isRecord(body) &&
    receipt.id === body.operationId &&
    body.reason === null &&
    receipt.reservationId === reservationId &&
    receipt.actorId === actorId
  );
}
