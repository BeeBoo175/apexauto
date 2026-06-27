import { handlerName, parseHandlerDisplay } from "@/lib/account/handler-display";
import { parseCountedPage, parseItems } from "@/lib/api";
import { parseActor } from "@/lib/auth/actor";
import { parseOwnershipKind } from "@/lib/staff/ownership";
import {
  isNonNegativeInteger,
  isPositiveInteger,
  isRecord,
  isUtcInstant,
  isUuid,
} from "@/lib/auth/contracts";
import { cancellationKinds, parseTestDriveSlot, testDriveStates } from "./customer";

const countKeys = [
  "all",
  "booked",
  "inProgress",
  "completed",
  "notCompleted",
  "noShow",
  "cancelled",
  "toClaim",
  "myWork",
  "history",
] as const;

export function parseTestDriveOwnership(value: unknown) {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !isUuid(value.bookingId) ||
    !isPositiveInteger(value.version) ||
    !isUuid(value.actorId) ||
    (value.handlerId !== null && !isUuid(value.handlerId)) ||
    !isUtcInstant(value.recordedAt) ||
    (value.reason !== null && typeof value.reason !== "string")
  ) {
    return null;
  }
  const kind = parseOwnershipKind(value.kind, value.handlerId);
  if (!kind) {
    return null;
  }
  return {
    id: value.id,
    bookingId: value.bookingId,
    version: value.version,
    actorId: value.actorId,
    handlerId: value.handlerId,
    kind,
    recordedAt: value.recordedAt,
    reason: value.reason,
  };
}

export function parseStaffTestDriveItem(value: unknown) {
  const slot = parseTestDriveSlot(value);
  if (
    !slot ||
    !isRecord(value) ||
    !isUuid(value.id) ||
    !isUuid(value.vehicleId) ||
    !isUuid(value.branchId) ||
    typeof value.branchName !== "string" ||
    typeof value.registrationPlate !== "string" ||
    typeof value.customerName !== "string" ||
    typeof value.make !== "string" ||
    typeof value.model !== "string" ||
    typeof value.trim !== "string" ||
    !isPositiveInteger(value.manufactureYear) ||
    !isNonNegativeInteger(value.ownershipVersion) ||
    (value.assignmentId !== null && !isUuid(value.assignmentId)) ||
    (value.handlerId !== null && !isUuid(value.handlerId)) ||
    (value.handlerEmail !== null && typeof value.handlerEmail !== "string") ||
    typeof value.handlerEligible !== "boolean" ||
    (value.ownershipVersion === 0) !== (value.assignmentId === null) ||
    (value.handlerId === null && (value.handlerEmail !== null || value.handlerEligible))
  ) {
    return null;
  }
  const status = testDriveStates.find((item) => item === value.status);
  const display = parseHandlerDisplay(value, "handlerName", "handlerPictureId");
  if (!status || !display || (value.ownershipVersion === 0 && value.handlerId !== null)) {
    return null;
  }
  return {
    ...slot,
    id: value.id,
    vehicleId: value.vehicleId,
    branchId: value.branchId,
    branchName: value.branchName,
    registrationPlate: value.registrationPlate,
    customerName: value.customerName,
    make: value.make,
    model: value.model,
    trim: value.trim,
    manufactureYear: value.manufactureYear,
    status,
    assignmentId: value.assignmentId,
    ownershipVersion: value.ownershipVersion,
    handlerId: value.handlerId,
    handlerEmail: value.handlerEmail,
    handlerName: display.name,
    handlerPictureId: display.pictureId,
    handlerEligible: value.handlerEligible,
  };
}

export function parseStaffTestDrivePage(value: unknown) {
  const page = parseCountedPage(value, parseStaffTestDriveItem, countKeys);
  const actor = isRecord(value) ? parseActor(value.actor) : null;
  if (!page || !page.counts || !actor || !hasTestDriveAccess(actor.role)) {
    return null;
  }
  return { ...page, actor };
}

export function hasTestDriveAccess(role: string) {
  return ["SalesAgent", "BranchManager", "GeneralManager"].includes(role);
}

export function parseTestDriveStart(value: unknown) {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !isUuid(value.bookingId) ||
    !isUuid(value.assignmentId) ||
    !isUuid(value.staffId) ||
    value.customerPresent !== true ||
    value.drivingLicenceChecked !== true ||
    !isUtcInstant(value.startedAt)
  ) {
    return null;
  }
  return {
    id: value.id,
    bookingId: value.bookingId,
    assignmentId: value.assignmentId,
    staffId: value.staffId,
    customerPresent: value.customerPresent,
    drivingLicenceChecked: value.drivingLicenceChecked,
    startedAt: value.startedAt,
  };
}

export function parseTestDriveOutcome(value: unknown) {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !isUuid(value.bookingId) ||
    !isUuid(value.assignmentId) ||
    !isUuid(value.staffId) ||
    (value.startId !== null && !isUuid(value.startId)) ||
    value.customerPresent !== true ||
    !isUtcInstant(value.endedAt) ||
    (value.reason !== null && typeof value.reason !== "string") ||
    (value.notes !== null && typeof value.notes !== "string")
  ) {
    return null;
  }
  const kind = ["Completed", "NotCompleted"].find((item) => item === value.kind);
  if (
    !kind ||
    (kind === "Completed" && (!value.startId || value.reason !== null)) ||
    (kind === "NotCompleted" && !value.reason)
  ) {
    return null;
  }
  return {
    id: value.id,
    bookingId: value.bookingId,
    assignmentId: value.assignmentId,
    staffId: value.staffId,
    startId: value.startId,
    customerPresent: value.customerPresent,
    kind,
    endedAt: value.endedAt,
    reason: value.reason,
    notes: value.notes,
  };
}

function parseCancellation(value: unknown) {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !isUuid(value.bookingId) ||
    !isUtcInstant(value.recordedAt) ||
    (value.staffId !== null && !isUuid(value.staffId)) ||
    (value.reason !== null && typeof value.reason !== "string")
  ) {
    return null;
  }
  const kind = cancellationKinds.find((item) => item === value.kind);
  if (!kind || (kind !== "CustomerCancelled" && !value.reason)) {
    return null;
  }
  return {
    id: value.id,
    bookingId: value.bookingId,
    kind,
    staffId: value.staffId,
    recordedAt: value.recordedAt,
    reason: value.reason,
  };
}

export function parseStaffTestDriveDetail(value: unknown) {
  if (!isRecord(value)) {
    return null;
  }
  const actor = parseActor(value.actor);
  const booking = parseStaffTestDriveItem(value.booking);
  const ownership = value.ownership === null ? null : parseTestDriveOwnership(value.ownership);
  const cancellation = value.cancellation === null ? null : parseCancellation(value.cancellation);
  const start = value.start === null ? null : parseTestDriveStart(value.start);
  const outcome = value.outcome === null ? null : parseTestDriveOutcome(value.outcome);
  if (
    !actor ||
    !hasTestDriveAccess(actor.role) ||
    !booking ||
    !isUtcInstant(value.createdAt) ||
    typeof value.customerMobile !== "string" ||
    typeof value.customerEmail !== "string" ||
    (value.startedBy !== null && typeof value.startedBy !== "string") ||
    (value.completedBy !== null && typeof value.completedBy !== "string") ||
    (value.ownership !== null && !ownership) ||
    (value.cancellation !== null && !cancellation) ||
    (value.start !== null && !start) ||
    (value.outcome !== null && !outcome)
  ) {
    return null;
  }
  const evidence = [ownership, cancellation, start, outcome];
  if (
    evidence.some((item) => item && item.bookingId !== booking.id) ||
    (ownership?.id ?? null) !== booking.assignmentId ||
    (ownership?.version ?? 0) !== booking.ownershipVersion ||
    (ownership?.handlerId ?? null) !== booking.handlerId ||
    (outcome && outcome.startId !== (start?.id ?? null)) ||
    (cancellation && (start || outcome)) ||
    (booking.status === "InProgress" && (!start || outcome)) ||
    (booking.status === "Cancelled" && !cancellation) ||
    (booking.status === "Completed" && outcome?.kind !== "Completed") ||
    (booking.status === "NotCompleted" && outcome?.kind !== "NotCompleted")
  ) {
    return null;
  }
  return {
    actor,
    booking,
    createdAt: value.createdAt,
    customerMobile: value.customerMobile,
    customerEmail: value.customerEmail,
    ownership,
    cancellation,
    start,
    outcome,
    startedBy: value.startedBy,
    completedBy: value.completedBy,
  };
}

export function parseTestDriveHandlers(value: unknown) {
  if (!isRecord(value)) {
    return null;
  }
  const actor = parseActor(value.actor);
  const items = parseItems(value.items, (item) => {
    return isRecord(item) && isUuid(item.id) && typeof item.email === "string" && item.email
      ? { id: item.id, email: item.email, name: handlerName(item.name, item.email) }
      : null;
  });
  return actor?.role === "BranchManager" && items ? { actor, items } : null;
}
