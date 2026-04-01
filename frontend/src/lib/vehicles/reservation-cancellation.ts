import { isRecord, isUuid } from "@/lib/auth/contracts";

export interface ReservationCancellationReceipt {
  id: string;
  reservationId: string;
  kind: "StaffCancelled";
  actorId: string;
  reason: string;
  recordedAt: string;
  refund: number;
}

export function reservationCancellationReason(value: string): string | null {
  const reason = value.trim();
  return reason && reason.length <= 1000 && !/\p{Cc}/u.test(value) ? reason : null;
}

export function parseReservationCancellation(
  value: unknown,
): ReservationCancellationReceipt | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !isUuid(value.reservationId) ||
    value.kind !== "StaffCancelled" ||
    !isUuid(value.actorId) ||
    typeof value.reason !== "string" ||
    reservationCancellationReason(value.reason) !== value.reason ||
    typeof value.recordedAt !== "string" ||
    !/(?:Z|\+00:00)$/.test(value.recordedAt) ||
    !Number.isFinite(Date.parse(value.recordedAt)) ||
    value.refund !== 500
  ) {
    return null;
  }
  return {
    id: value.id,
    reservationId: value.reservationId,
    kind: value.kind,
    actorId: value.actorId,
    reason: value.reason,
    recordedAt: value.recordedAt,
    refund: value.refund,
  };
}

export function matchesReservationCancellation(
  receipt: ReservationCancellationReceipt,
  body: unknown,
  reservationId: string,
  actorId: string,
  deposit: number,
) {
  return (
    isRecord(body) &&
    receipt.id === body.operationId &&
    receipt.reservationId === reservationId &&
    receipt.actorId === actorId &&
    receipt.kind === "StaffCancelled" &&
    receipt.reason === body.reason &&
    receipt.refund === deposit
  );
}
