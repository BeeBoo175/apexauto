import {
  isNonNegativeInteger,
  isPositiveInteger,
  isRecord,
  isUtcInstant,
  isUuid,
} from "@/lib/auth/contracts";
import { parseItems } from "@/lib/api";
import {
  parseAssessment,
  parseEntry,
  parsePoint,
  ids,
  versions,
  money,
  text,
  nullableId,
  nullableTime,
} from "./parse";
import type {
  Approval,
  Assignment,
  Completion,
  PhotoReceipt,
  RepairRequest,
  Reopening,
  Report,
  ReportPhoto,
  ReportPoint,
  RevisionHistory,
  RevisionSummary,
  RepairHistory,
  RepairHistoryEntry,
} from "./report-contracts";

function parseReportPhoto(v: unknown): ReportPhoto | null {
  if (
    !isRecord(v) ||
    !isUuid(v.id) ||
    !["image/jpeg", "image/png", "image/webp"].includes(String(v.contentType)) ||
    !versions(v, ["bytes", "width", "height"]) ||
    !text(v.sha256)
  ) {
    return null;
  }
  return { ...v } as ReportPhoto;
}

function parseReportPoint(v: unknown): ReportPoint | null {
  const point = parsePoint(v);
  if (!point || !isRecord(v) || !money(v.estimatedRepairCost)) {
    return null;
  }
  const photos = parseItems(v.photos, parseReportPhoto);
  const entry = v.kind === null ? null : parseEntry({ ...v, pointCode: v.code });
  if (
    !photos ||
    (v.kind === null && (v.severity !== null || v.notes !== null)) ||
    (v.kind !== null && !entry)
  ) {
    return null;
  }
  return {
    ...v,
    ...point,
    kind: entry?.kind ?? null,
    severity: entry?.severity ?? null,
    photos,
  } as ReportPoint;
}

export function parseReport(v: unknown): Report | null {
  if (
    !isRecord(v) ||
    !ids(v, ["operationId", "inspectionId", "branchId", "saveId", "technicianId"]) ||
    !versions(v, ["revision", "saveVersion", "assignmentVersion"]) ||
    !(v.technicianIdentity == null || text(v.technicianIdentity)) ||
    !text(v.catalogVersion) ||
    !isUtcInstant(v.signedAt)
  ) {
    return null;
  }
  const assessment = parseAssessment(v.assessment);
  const checklist = parseItems(v.checklist, parseReportPoint);
  const vehicle = v.vehicle;
  if (
    !assessment ||
    !checklist ||
    !checklist.length ||
    !isRecord(vehicle) ||
    !ids(vehicle, ["id", "intakeAppointmentId", "intakeOwnerId"]) ||
    !["vin", "registrationPlate", "make", "model"].every((key) => text(vehicle[key])) ||
    typeof vehicle.trim !== "string" ||
    !isPositiveInteger(vehicle.manufactureYear) ||
    !isNonNegativeInteger(vehicle.mileageKilometres) ||
    !isUtcInstant(vehicle.checkedInAt)
  ) {
    return null;
  }
  return {
    ...v,
    technicianIdentity: v.technicianIdentity ?? null,
    assessment,
    checklist,
    vehicle,
  } as Report;
}

export function parseAssignment(v: unknown): Assignment | null {
  if (
    !isRecord(v) ||
    !ids(v, ["operationId", "inspectionId", "vehicleId", "branchId", "actorId", "technicianId"]) ||
    !nullableId(v.previousTechnicianId) ||
    !isNonNegativeInteger(v.expectedVersion) ||
    !isPositiveInteger(v.version) ||
    !(v.reason === null || text(v.reason)) ||
    !isUtcInstant(v.recordedAt)
  ) {
    return null;
  }
  return { ...v } as Assignment;
}

export function parseReopening(v: unknown): Reopening | null {
  if (
    !isRecord(v) ||
    !ids(v, ["operationId", "inspectionId", "branchId", "actorId", "signedRevisionId"]) ||
    !versions(v, ["assignmentVersion", "expectedRevision", "revision"]) ||
    !isNonNegativeInteger(v.afterSaveVersion) ||
    !text(v.reason) ||
    !isUtcInstant(v.recordedAt)
  ) {
    return null;
  }
  return { ...v } as Reopening;
}

export function parseRepairRequest(v: unknown): RepairRequest | null {
  if (
    !isRecord(v) ||
    !ids(v, ["operationId", "inspectionId", "branchId", "actorId", "signedRevisionId"]) ||
    !versions(v, ["assignmentVersion", "revision", "version"]) ||
    !isNonNegativeInteger(v.expectedVersion) ||
    !money(v.budget) ||
    !isUtcInstant(v.requestedAt)
  ) {
    return null;
  }
  const items = parseItems(v.items, (item) => {
    if (!isRecord(item) || !text(item.pointCode) || !text(item.work)) {
      return null;
    }
    return { pointCode: item.pointCode, work: item.work };
  });
  return items?.length ? ({ ...v, items } as RepairRequest) : null;
}

function repairReceipt(v: Record<string, unknown>) {
  return (
    ids(v, ["operationId", "inspectionId", "branchId", "actorId", "requestId"]) &&
    versions(v, ["assignmentVersion", "revision", "repairVersion"])
  );
}

export function parseApproval(v: unknown): Approval | null {
  if (!isRecord(v) || !repairReceipt(v) || !isUtcInstant(v.approvedAt)) {
    return null;
  }
  return { ...v } as Approval;
}

export function parseCompletion(v: unknown): Completion | null {
  if (
    !isRecord(v) ||
    !repairReceipt(v) ||
    !nullableId(v.approvalId) ||
    !text(v.notes) ||
    !isUtcInstant(v.completedAt)
  ) {
    return null;
  }
  return { ...v } as Completion;
}

export function parsePhoto(v: unknown): PhotoReceipt | null {
  if (
    !isRecord(v) ||
    !ids(v, ["id", "inspectionId", "actorId"]) ||
    !versions(v, ["assignmentVersion", "bytes", "width", "height"]) ||
    !text(v.pointCode) ||
    !["image/jpeg", "image/png", "image/webp"].includes(String(v.contentType)) ||
    !isUtcInstant(v.createdAt) ||
    !isUtcInstant(v.expiresAt) ||
    !nullableTime(v.availableAt) ||
    !nullableTime(v.removalRequestedAt) ||
    !nullableTime(v.deletedAt)
  ) {
    return null;
  }
  return { ...v } as PhotoReceipt;
}

function parseRevision(v: unknown): RevisionSummary | null {
  if (
    !isRecord(v) ||
    typeof v.hasRepairRequests !== "boolean" ||
    !ids(v, ["operationId", "technicianId"]) ||
    !versions(v, ["revision", "saveVersion", "assignmentVersion"]) ||
    !(v.technicianIdentity == null || text(v.technicianIdentity)) ||
    !text(v.catalogVersion) ||
    !isUtcInstant(v.signedAt)
  ) {
    return null;
  }
  const assessment = parseAssessment(v.assessment);
  const reopening = v.reopening === null ? null : parseReopening(v.reopening);
  if (!assessment || (v.reopening !== null && !reopening)) {
    return null;
  }
  return {
    ...v,
    technicianIdentity: v.technicianIdentity ?? null,
    assessment,
    reopening,
  } as RevisionSummary;
}

function historyPage(v: Record<string, unknown>) {
  return (
    isUuid(v.inspectionId) &&
    isNonNegativeInteger(v.totalCount) &&
    isPositiveInteger(v.page) &&
    [10, 25, 50].includes(Number(v.pageSize))
  );
}

export function parseRevisions(v: unknown): RevisionHistory | null {
  if (
    !isRecord(v) ||
    !historyPage(v) ||
    !isPositiveInteger(v.currentRevision) ||
    typeof v.isSigned !== "boolean"
  ) {
    return null;
  }
  const items = parseItems(v.items, parseRevision);
  return items ? ({ ...v, items } as RevisionHistory) : null;
}

function parseRepairHistoryEntry(v: unknown): RepairHistoryEntry | null {
  if (!isRecord(v)) {
    return null;
  }
  const request = parseRepairRequest(v.request);
  const approval = v.approval === null ? null : parseApproval(v.approval);
  const completion = v.completion === null ? null : parseCompletion(v.completion);
  if (!request || (v.approval !== null && !approval) || (v.completion !== null && !completion)) {
    return null;
  }
  return { request, approval, completion };
}

export function parseRepairs(v: unknown): RepairHistory | null {
  if (!isRecord(v) || !historyPage(v) || !isPositiveInteger(v.revision)) {
    return null;
  }
  const items = parseItems(v.items, parseRepairHistoryEntry);
  return items ? ({ ...v, items } as RepairHistory) : null;
}
