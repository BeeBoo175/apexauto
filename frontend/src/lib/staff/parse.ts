import { handlerName, parseHandlerDisplay } from "@/lib/account/handler-display";
import { parseCountedPage, parseItems } from "@/lib/api";
import { isNonNegativeInteger, isPositiveInteger, isRecord, isUuid } from "@/lib/auth/contracts";
import { parseSlot, parseVehicle } from "@/lib/intake/parse";
import { parseOwnershipKind } from "./ownership";
import {
  intakeCountKeys,
  type AppointmentDetail,
  type AppointmentHandler,
  type AppointmentOwnership,
  type AppointmentSummary,
  type CheckInReceipt,
  type WorklistItem,
} from "./contracts";

export function parseWorklistPage(value: unknown) {
  return parseCountedPage(value, parseWorklistItem, intakeCountKeys);
}

export function parseWorklistItem(value: unknown): WorklistItem | null {
  const row = parseAppointmentSummary(value);
  if (
    !row ||
    !isRecord(value) ||
    !validHandler(value) ||
    !isNonNegativeInteger(value.ownershipVersion) ||
    (value.ownershipVersion === 0 && value.handlerId !== null)
  ) {
    return null;
  }
  const display = parseHandlerDisplay(value, "handlerName", "handlerPictureId");
  if (!display) {
    return null;
  }
  return {
    ...row,
    handlerId: value.handlerId,
    handlerEmail: value.handlerEmail,
    handlerName: display.name,
    handlerPictureId: display.pictureId,
    handlerEligible: value.handlerEligible,
    handlerSuspended: value.handlerSuspended === true,
    customerSuspended: value.customerSuspended === true,
    ownershipVersion: value.ownershipVersion,
  };
}

function parseAppointmentSummary(value: unknown): AppointmentSummary | null {
  if (!isRecord(value) || !isUuid(value.id) || !isUuid(value.branchId)) {
    return null;
  }
  if (
    typeof value.branchName !== "string" ||
    typeof value.customerName !== "string" ||
    typeof value.registrationPlate !== "string" ||
    typeof value.make !== "string" ||
    !value.make.trim() ||
    typeof value.model !== "string" ||
    !value.model.trim() ||
    typeof value.trim !== "string" ||
    !value.trim.trim() ||
    !Number.isSafeInteger(value.manufactureYear) ||
    Number(value.manufactureYear) < 1
  ) {
    return null;
  }
  const status = ["Booked", "CheckedIn", "Cancelled", "NoShow"] as const;
  const state = status.find((item) => item === value.status);
  const slot = parseSlot(value);
  if (!state || !slot) {
    return null;
  }
  return {
    id: value.id,
    branchId: value.branchId,
    branchName: value.branchName,
    customerName: value.customerName,
    registrationPlate: value.registrationPlate,
    make: value.make,
    model: value.model,
    trim: value.trim,
    manufactureYear: Number(value.manufactureYear),
    status: state,
    ...slot,
  };
}

export function parseDetail(value: unknown): AppointmentDetail | null {
  const row = parseAppointmentSummary(value);
  const vehicle = parseVehicle(value);
  if (!row || !vehicle || !isRecord(value)) {
    return null;
  }
  const ownership = value.ownership === null ? null : parseOwnership(value.ownership);
  if (
    (value.ownership !== null && !ownership) ||
    (value.noShowAt !== null && !utcTimestamp(value.noShowAt)) ||
    (value.attendanceCorrectedAt !== null && !utcTimestamp(value.attendanceCorrectedAt)) ||
    (value.correctionReason !== null && typeof value.correctionReason !== "string") ||
    (value.correctedByName !== null &&
      !(typeof value.correctedByName === "string" && value.correctedByName)) ||
    typeof value.customerMobile !== "string" ||
    typeof value.customerEmail !== "string"
  ) {
    return null;
  }
  return {
    ...row,
    ...vehicle,
    ownership,
    noShowAt: value.noShowAt,
    attendanceCorrectedAt: value.attendanceCorrectedAt,
    correctionReason: value.correctionReason,
    correctedByName: value.correctedByName,
    customerMobile: value.customerMobile,
    customerEmail: value.customerEmail,
    customerSuspended: value.customerSuspended === true,
  };
}

function validHandler(value: Record<string, unknown>): value is Record<string, unknown> & {
  handlerId: string | null;
  handlerEmail: string | null;
  handlerEligible: boolean;
} {
  return (
    (value.handlerId === null || isUuid(value.handlerId)) &&
    (value.handlerEmail === null ||
      (typeof value.handlerEmail === "string" && !!value.handlerEmail.trim())) &&
    typeof value.handlerEligible === "boolean" &&
    (value.handlerId !== null || (value.handlerEmail === null && !value.handlerEligible))
  );
}

function parseOwnership(value: unknown): AppointmentOwnership | null {
  if (
    !isRecord(value) ||
    !validHandler(value) ||
    !isPositiveInteger(value.version) ||
    !isUuid(value.actorId) ||
    !utcTimestamp(value.recordedAt) ||
    (value.reason !== null && !(typeof value.reason === "string" && !!value.reason.trim()))
  ) {
    return null;
  }
  const kind = parseOwnershipKind(value.kind, value.handlerId);
  const display = parseHandlerDisplay(value, "handlerName", "handlerPictureId");
  if (!kind || !display) {
    return null;
  }
  return {
    version: value.version,
    handlerId: value.handlerId,
    handlerEmail: value.handlerEmail,
    handlerName: display.name,
    handlerPictureId: display.pictureId,
    handlerEligible: value.handlerEligible,
    handlerSuspended: value.handlerSuspended === true,
    actorId: value.actorId,
    kind,
    recordedAt: value.recordedAt,
    reason: value.reason,
  };
}

export function parseAppointmentHandlers(value: unknown): AppointmentHandler[] | null {
  const handlers = parseItems(value, (item): AppointmentHandler | null => {
    if (
      !isRecord(item) ||
      !isUuid(item.id) ||
      typeof item.email !== "string" ||
      !item.email.trim()
    ) {
      return null;
    }
    return { id: item.id, email: item.email, name: handlerName(item.name, item.email) };
  });
  return handlers && new Set(handlers.map((item) => item.id)).size === handlers.length
    ? handlers
    : null;
}

export function parseCheckIn(value: unknown): CheckInReceipt | null {
  if (
    !isRecord(value) ||
    !isUuid(value.appointmentId) ||
    !isUuid(value.vehicleId) ||
    !isUuid(value.staffId)
  ) {
    return null;
  }
  if (
    typeof value.checkedInAt !== "string" ||
    !/(Z|\+00:00)$/.test(value.checkedInAt) ||
    !Number.isFinite(Date.parse(value.checkedInAt))
  ) {
    return null;
  }
  const evidence = value.evidence;
  if (
    !isRecord(evidence) ||
    typeof evidence.vin !== "string" ||
    typeof evidence.mileageKilometres !== "number" ||
    !Number.isSafeInteger(evidence.mileageKilometres) ||
    evidence.mileageKilometres < 0
  ) {
    return null;
  }
  if (
    evidence.customerPresent !== true ||
    evidence.identityConfirmed !== true ||
    evidence.ownershipConfirmed !== true ||
    evidence.vehicleConfirmed !== true
  ) {
    return null;
  }
  if (evidence.notes !== null && typeof evidence.notes !== "string") {
    return null;
  }
  let correction = null;
  if (value.correction !== null) {
    if (
      !isRecord(value.correction) ||
      !utcTimestamp(value.correction.recordedAt) ||
      typeof value.correction.reason !== "string" ||
      !value.correction.reason.trim()
    ) {
      return null;
    }
    correction = { recordedAt: value.correction.recordedAt, reason: value.correction.reason };
  }
  return {
    correction,
    appointmentId: value.appointmentId,
    vehicleId: value.vehicleId,
    staffId: value.staffId,
    checkedInAt: value.checkedInAt,
    evidence: {
      vin: evidence.vin,
      mileageKilometres: evidence.mileageKilometres,
      customerPresent: true,
      identityConfirmed: true,
      ownershipConfirmed: true,
      vehicleConfirmed: true,
      notes: evidence.notes,
    },
  };
}

export function checkInMatches(receipt: CheckInReceipt, request: unknown) {
  if (!isRecord(request)) {
    return false;
  }
  return (
    receipt.evidence.vin === String(request.vin).trim().toUpperCase() &&
    receipt.evidence.mileageKilometres === request.mileageKilometres &&
    receipt.evidence.notes === (String(request.notes ?? "").trim() || null) &&
    ["customerPresent", "identityConfirmed", "ownershipConfirmed", "vehicleConfirmed"].every(
      (key) => request[key] === true,
    )
  );
}

function utcTimestamp(value: unknown): value is string {
  return (
    typeof value === "string" && /(Z|\+00:00)$/.test(value) && Number.isFinite(Date.parse(value))
  );
}

export function correctionMatches(receipt: CheckInReceipt, request: unknown) {
  if (!isRecord(request) || typeof request.attendedAt !== "string") {
    return false;
  }
  return (
    checkInMatches(receipt, request) &&
    Date.parse(receipt.checkedInAt) === Date.parse(request.attendedAt) &&
    receipt.correction?.reason === String(request.reason ?? "").trim()
  );
}
