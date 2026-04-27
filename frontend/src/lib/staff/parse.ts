import { parseCountedPage } from "@/lib/api";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { parseSlot, parseVehicle } from "@/lib/intake/parse";
import {
  intakeCountKeys,
  type AppointmentDetail,
  type CheckInReceipt,
  type WorklistItem,
} from "./contracts";

export function parseWorklistPage(value: unknown) {
  return parseCountedPage(value, parseWorklistItem, intakeCountKeys);
}

export function parseWorklistItem(value: unknown): WorklistItem | null {
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
  const row = parseWorklistItem(value);
  const vehicle = parseVehicle(value);
  if (!row || !vehicle || !isRecord(value)) {
    return null;
  }
  if (
    (value.noShowAt !== null && !utcTimestamp(value.noShowAt)) ||
    (value.attendanceCorrectedAt !== null && !utcTimestamp(value.attendanceCorrectedAt)) ||
    (value.correctionReason !== null && typeof value.correctionReason !== "string") ||
    (value.correctedBy !== null && !isUuid(value.correctedBy))
  ) {
    return null;
  }
  return {
    ...row,
    ...vehicle,
    noShowAt: value.noShowAt,
    attendanceCorrectedAt: value.attendanceCorrectedAt,
    correctionReason: value.correctionReason,
    correctedBy: value.correctedBy,
  };
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
