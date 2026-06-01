import { parseItems, parsePage } from "@/lib/api";
import { parseActor } from "@/lib/auth/actor";
import { isPositiveInteger, isRecord, isUtcInstant, isUuid } from "@/lib/auth/contracts";
import { malaysiaInput } from "@/lib/date-format";
import { parseSlot, parseSlots } from "@/lib/intake/parse";
import { ACCOUNT_PAGE_SIZE } from "@/lib/pagination";

export const testDriveStates = [
  "Booked",
  "InProgress",
  "Completed",
  "NotCompleted",
  "NoShow",
  "Cancelled",
] as const;

export const cancellationKinds = [
  "CustomerCancelled",
  "StaffCancelled",
  "VehicleReserved",
  "ListingWithdrawn",
  "InspectionReopened",
  "InspectionRepairRequested",
] as const;

export function parseTestDriveSlot(value: unknown) {
  const slot = parseSlot(value);
  return slot && Date.parse(slot.endsAt) - Date.parse(slot.startsAt) === 30 * 60 * 1000
    ? slot
    : null;
}

export function parseTestDriveSlots(value: unknown) {
  const offer = parseSlots(value);
  if (!offer || !isRecord(value) || !isUuid(value.vehicleId) || !isUuid(value.publicationId)) {
    return null;
  }
  const slots = parseItems(value.slots, parseTestDriveSlot);
  if (
    !slots ||
    offer.date < offer.dateWindow.firstDate ||
    offer.date > offer.dateWindow.lastDate ||
    slots.some((slot) => malaysiaInput(slot.startsAt).slice(0, 10) !== offer.date) ||
    new Set(slots.map((slot) => slot.startsAt)).size !== slots.length
  ) {
    return null;
  }
  return { ...offer, slots, vehicleId: value.vehicleId, publicationId: value.publicationId };
}

export function parseTestDriveRequest(value: unknown) {
  if (
    !isRecord(value) ||
    !isUuid(value.operationId) ||
    !isUuid(value.publicationId) ||
    !isUtcInstant(value.startsAt)
  ) {
    return null;
  }
  return {
    operationId: value.operationId,
    publicationId: value.publicationId,
    startsAt: value.startsAt,
  };
}

export function parseTestDriveBooking(value: unknown) {
  const slot = parseTestDriveSlot(value);
  if (
    !slot ||
    !isRecord(value) ||
    !isUuid(value.id) ||
    !isUuid(value.customerId) ||
    !isUuid(value.vehicleId) ||
    !isUuid(value.branchId) ||
    !isUuid(value.publicationId) ||
    !isUtcInstant(value.createdAt) ||
    Date.parse(value.createdAt) >= Date.parse(slot.startsAt)
  ) {
    return null;
  }
  return {
    ...slot,
    id: value.id,
    customerId: value.customerId,
    vehicleId: value.vehicleId,
    branchId: value.branchId,
    publicationId: value.publicationId,
    createdAt: value.createdAt,
  };
}

export function matchesTestDriveBooking(
  receipt: NonNullable<ReturnType<typeof parseTestDriveBooking>>,
  request: NonNullable<ReturnType<typeof parseTestDriveRequest>>,
  vehicleId: string,
  customerId: string,
) {
  return (
    receipt.id === request.operationId &&
    receipt.customerId === customerId &&
    receipt.vehicleId === vehicleId &&
    receipt.publicationId === request.publicationId &&
    Date.parse(receipt.startsAt) === Date.parse(request.startsAt)
  );
}

export function parseTestDriveCancellation(value: unknown) {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !isUuid(value.bookingId) ||
    !isUuid(value.customerId) ||
    !isUtcInstant(value.recordedAt) ||
    (value.staffId !== null && !isUuid(value.staffId)) ||
    (value.reason !== null && typeof value.reason !== "string")
  ) {
    return null;
  }
  const kind = cancellationKinds.find((item) => item === value.kind);
  if (
    !kind ||
    (kind === "CustomerCancelled" && (value.reason !== null || value.staffId !== null)) ||
    (kind !== "CustomerCancelled" && !value.reason) ||
    (kind === "StaffCancelled" && !isUuid(value.staffId))
  ) {
    return null;
  }
  return {
    id: value.id,
    bookingId: value.bookingId,
    customerId: value.customerId,
    kind,
    recordedAt: value.recordedAt,
    staffId: value.staffId,
    reason: value.reason,
  };
}

export function parseTestDriveHistoryItem(value: unknown) {
  const slot = parseTestDriveSlot(value);
  if (
    !slot ||
    !isRecord(value) ||
    !isUuid(value.id) ||
    !isUuid(value.vehicleId) ||
    !isUuid(value.branchId) ||
    typeof value.branchName !== "string" ||
    typeof value.make !== "string" ||
    typeof value.model !== "string" ||
    typeof value.trim !== "string" ||
    !isPositiveInteger(value.manufactureYear) ||
    !isUtcInstant(value.createdAt) ||
    (value.startedAt !== null && !isUtcInstant(value.startedAt)) ||
    (value.endedAt !== null && !isUtcInstant(value.endedAt)) ||
    (value.cancelledAt !== null && !isUtcInstant(value.cancelledAt)) ||
    (value.cancellationReason !== null && typeof value.cancellationReason !== "string") ||
    (value.outcomeReason !== null && typeof value.outcomeReason !== "string")
  ) {
    return null;
  }
  const status = testDriveStates.find((item) => item === value.status);
  if (
    !status ||
    (status === "InProgress" && (!value.startedAt || value.endedAt || value.cancelledAt)) ||
    (status === "Cancelled" && !value.cancelledAt) ||
    (status === "Completed" && (!value.startedAt || !value.endedAt)) ||
    (status === "NotCompleted" && (!value.endedAt || !value.outcomeReason))
  ) {
    return null;
  }
  if (
    (value.cancelledAt !== null && (status !== "Cancelled" || value.startedAt || value.endedAt)) ||
    (value.endedAt !== null && status !== "Completed" && status !== "NotCompleted") ||
    ((status === "Booked" || status === "NoShow") &&
      (value.startedAt || value.endedAt || value.cancelledAt)) ||
    (value.startedAt !== null && Date.parse(value.startedAt) < Date.parse(value.createdAt)) ||
    (value.endedAt !== null &&
      Date.parse(value.endedAt) < Date.parse(value.startedAt ?? value.createdAt))
  ) {
    return null;
  }
  return {
    ...slot,
    id: value.id,
    vehicleId: value.vehicleId,
    branchId: value.branchId,
    branchName: value.branchName,
    make: value.make,
    model: value.model,
    trim: value.trim,
    manufactureYear: value.manufactureYear,
    createdAt: value.createdAt,
    status,
    startedAt: value.startedAt,
    endedAt: value.endedAt,
    cancelledAt: value.cancelledAt,
    cancellationReason: value.cancellationReason,
    outcomeReason: value.outcomeReason,
  };
}

export function parseTestDriveHistory(value: unknown) {
  const page = parsePage(value, parseTestDriveHistoryItem, ACCOUNT_PAGE_SIZE);
  const actor = isRecord(value) ? parseActor(value.actor) : null;
  if (!page || actor?.role !== "Customer") {
    return null;
  }
  return { ...page, actor };
}
