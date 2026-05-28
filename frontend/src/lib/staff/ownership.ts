import {
  isNonNegativeInteger,
  isPositiveInteger,
  isRecord,
  isUtcInstant,
  isUuid,
} from "@/lib/auth/contracts";

export type OwnershipAction = "claims" | "assignments" | "releases";

export interface OwnershipReceipt {
  id: string;
  appointmentId: string;
  branchId: string;
  actorId: string;
  kind: "Claimed" | "Assigned" | "Released";
  handlerId: string | null;
  previousHandlerId: string | null;
  expectedVersion: number;
  version: number;
  reason: string | null;
  recordedAt: string;
}

export interface OwnershipResult {
  receipt: OwnershipReceipt;
  current: OwnershipReceipt;
}

export function parseOwnershipReceipt(value: unknown): OwnershipReceipt | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !isUuid(value.appointmentId) ||
    !isUuid(value.branchId) ||
    !isUuid(value.actorId) ||
    !(value.handlerId === null || isUuid(value.handlerId)) ||
    !(value.previousHandlerId === null || isUuid(value.previousHandlerId)) ||
    !isNonNegativeInteger(value.expectedVersion) ||
    !isPositiveInteger(value.version) ||
    value.version !== value.expectedVersion + 1 ||
    !isUtcInstant(value.recordedAt) ||
    !(value.reason === null || validReason(value.reason))
  ) {
    return null;
  }
  const kind = (["Claimed", "Assigned", "Released"] as const).find(
    (item) => item === value.kind,
  );
  if (!kind || !validTransition(value, kind)) {
    return null;
  }
  return {
    id: value.id,
    appointmentId: value.appointmentId,
    branchId: value.branchId,
    actorId: value.actorId,
    kind,
    handlerId: value.handlerId,
    previousHandlerId: value.previousHandlerId,
    expectedVersion: value.expectedVersion,
    version: value.version,
    reason: value.reason,
    recordedAt: value.recordedAt,
  };
}

function validReason(value: unknown): value is string {
  return (
    typeof value === "string" &&
    value.trim().length > 0 &&
    value.length <= 1000 &&
    !/[\x00-\x1f\x7f-\x9f]/.test(value)
  );
}

function validTransition(value: Record<string, unknown>, kind: OwnershipReceipt["kind"]) {
  if (kind === "Claimed") {
    return (
      value.handlerId === value.actorId &&
      value.previousHandlerId === null &&
      value.reason === null
    );
  }
  if (kind === "Released") {
    return value.handlerId === null && value.previousHandlerId !== null;
  }
  return (
    value.handlerId !== null &&
    value.handlerId !== value.previousHandlerId &&
    (value.previousHandlerId === null || value.reason !== null)
  );
}

export function parseOwnershipResult(value: unknown): OwnershipResult | null {
  if (!isRecord(value)) {
    return null;
  }
  const receipt = parseOwnershipReceipt(value.receipt);
  const current = parseOwnershipReceipt(value.current);
  if (
    !receipt ||
    !current ||
    receipt.appointmentId !== current.appointmentId ||
    receipt.branchId !== current.branchId ||
    current.version < receipt.version ||
    (current.version === receipt.version && current.id !== receipt.id)
  ) {
    return null;
  }
  return { receipt, current };
}

export function ownershipMatches(
  result: OwnershipResult,
  request: unknown,
  appointmentId: string,
  actorId: string,
  action: OwnershipAction,
) {
  if (!isRecord(request)) {
    return false;
  }
  const kinds = { claims: "Claimed", assignments: "Assigned", releases: "Released" } as const;
  const receipt = result.receipt;
  let handlerId: unknown = null;
  if (action === "claims") {
    handlerId = actorId;
  }
  if (action === "assignments") {
    handlerId = request.handlerId;
  }
  return (
    receipt.id === request.operationId &&
    receipt.appointmentId === appointmentId &&
    receipt.actorId === actorId &&
    receipt.kind === kinds[action] &&
    receipt.handlerId === handlerId &&
    receipt.expectedVersion === request.expectedVersion &&
    receipt.reason === (String(request.reason ?? "").trim() || null)
  );
}
