import type { CurrentActor } from "@/lib/auth/actor";
import { isNonNegativeInteger, isRecord, isUuid } from "@/lib/auth/contracts";
import { parseTestDriveCancellation } from "./customer";
import { parseTestDriveOutcome, parseTestDriveOwnership, parseTestDriveStart } from "./staff";

export type TestDriveStaffCommand =
  "claims" | "assignments" | "releases" | "cancellations" | "starts" | "outcomes";

function optionalText(value: unknown) {
  if (value === null || value === undefined) {
    return null;
  }
  return typeof value === "string" &&
    value.trim().length > 0 &&
    value.trim().length <= 1000 &&
    !/[\x00-\x1f\x7f-\x9f]/.test(value)
    ? value.trim()
    : undefined;
}

export function shapeTestDriveStaffCommand(
  command: TestDriveStaffCommand,
  value: unknown,
): Record<string, unknown> | null {
  if (!isRecord(value) || !isUuid(value.operationId)) {
    return null;
  }
  const operationId = value.operationId;
  const reason = optionalText(value.reason);
  if (reason === undefined) {
    return null;
  }
  if (command === "cancellations") {
    return reason ? { operationId, reason } : null;
  }
  if (command === "starts") {
    return isUuid(value.assignmentId) &&
      value.customerPresent === true &&
      value.drivingLicenceChecked === true
      ? {
          operationId,
          assignmentId: value.assignmentId,
          customerPresent: true,
          drivingLicenceChecked: true,
        }
      : null;
  }
  if (command === "outcomes") {
    const notes = optionalText(value.notes);
    const validKind =
      (value.kind === "Completed" && reason === null) ||
      (value.kind === "NotCompleted" && reason !== null);
    return isUuid(value.assignmentId) &&
      value.customerPresent === true &&
      notes !== undefined &&
      validKind
      ? {
          operationId,
          assignmentId: value.assignmentId,
          customerPresent: true,
          kind: value.kind,
          reason,
          notes,
        }
      : null;
  }
  if (
    !isNonNegativeInteger(value.expectedVersion) ||
    value.expectedVersion >= Number.MAX_SAFE_INTEGER
  ) {
    return null;
  }
  const expectedVersion = value.expectedVersion;
  if (command === "assignments") {
    return isUuid(value.handlerId)
      ? { operationId, expectedVersion, handlerId: value.handlerId, reason }
      : null;
  }
  if (value.handlerId !== undefined && value.handlerId !== null) {
    return null;
  }
  if (command === "claims" && reason !== null) {
    return null;
  }
  return { operationId, expectedVersion, handlerId: null, reason };
}

export function parseTestDriveStaffReceipt(
  command: TestDriveStaffCommand,
  value: unknown,
  body: Record<string, unknown>,
  bookingId: string,
  actor: CurrentActor,
) {
  if (command === "cancellations") {
    const receipt = parseTestDriveCancellation(value);
    return receipt &&
      receipt.id === body.operationId &&
      receipt.bookingId === bookingId &&
      receipt.staffId === actor.userId &&
      receipt.kind === "StaffCancelled" &&
      receipt.reason === body.reason
      ? { cancellation: receipt }
      : null;
  }
  if (!isRecord(value)) {
    return null;
  }
  if (command === "starts" || command === "outcomes") {
    return parseHandlingReceipt(command, value, body, bookingId, actor.userId);
  }
  const receipt = parseTestDriveOwnership(value.receipt);
  const current = parseTestDriveOwnership(value.current);
  const kinds = { claims: "Claimed", assignments: "Assigned", releases: "Released" };
  const handlerId = command === "claims" ? actor.userId : body.handlerId;
  if (
    !receipt ||
    !current ||
    receipt.id !== body.operationId ||
    receipt.bookingId !== bookingId ||
    receipt.actorId !== actor.userId ||
    receipt.kind !== kinds[command] ||
    receipt.version !== Number(body.expectedVersion) + 1 ||
    receipt.handlerId !== handlerId ||
    receipt.reason !== body.reason ||
    current.bookingId !== bookingId ||
    current.version < receipt.version
  ) {
    return null;
  }
  return { receipt, current };
}

function parseHandlingReceipt(
  command: "starts" | "outcomes",
  value: Record<string, unknown>,
  body: Record<string, unknown>,
  bookingId: string,
  actorId: string,
) {
  const start = value.start === null ? null : parseTestDriveStart(value.start);
  const outcome = value.outcome === null ? null : parseTestDriveOutcome(value.outcome);
  if (
    (value.start !== null && !start) ||
    (value.outcome !== null && !outcome) ||
    (start && start.bookingId !== bookingId) ||
    (outcome && outcome.bookingId !== bookingId) ||
    (outcome && outcome.startId !== (start?.id ?? null))
  ) {
    return null;
  }
  if (command === "starts") {
    return start &&
      start.id === body.operationId &&
      start.assignmentId === body.assignmentId &&
      start.staffId === actorId
      ? { start, outcome }
      : null;
  }
  return outcome &&
    outcome.id === body.operationId &&
    outcome.assignmentId === body.assignmentId &&
    outcome.staffId === actorId &&
    outcome.kind === body.kind &&
    outcome.reason === body.reason &&
    outcome.notes === body.notes
    ? { start, outcome }
    : null;
}
