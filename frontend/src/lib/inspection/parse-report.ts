import { isRecord, isUuid } from "@/lib/auth/contracts";
import { parseItems } from "@/lib/intake/parse";
import {
  parseAssessment,
  parseEntry,
  parsePoint,
  ids,
  versions,
  integer,
  positive,
  money,
  text,
  timestamp,
  nullableId,
} from "./parse";
import type {
  Approval,
  Assignment,
  Completion,
  RepairRequest,
  Reopening,
  Report,
  ReportPhoto,
  ReportPoint,
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
    !text(v.technicianIdentity) ||
    !text(v.catalogVersion) ||
    !timestamp(v.signedAt)
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
    !positive(vehicle.manufactureYear) ||
    !integer(vehicle.mileageKilometres) ||
    !timestamp(vehicle.checkedInAt)
  ) {
    return null;
  }
  return { ...v, assessment, checklist, vehicle } as Report;
}

export function parseAssignment(v: unknown): Assignment | null {
  if (
    !isRecord(v) ||
    !ids(v, ["operationId", "inspectionId", "vehicleId", "branchId", "actorId", "technicianId"]) ||
    !nullableId(v.previousTechnicianId) ||
    !integer(v.expectedVersion) ||
    !positive(v.version) ||
    !(v.reason === null || text(v.reason)) ||
    !timestamp(v.recordedAt)
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
    !integer(v.afterSaveVersion) ||
    !text(v.reason) ||
    !timestamp(v.recordedAt)
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
    !integer(v.expectedVersion) ||
    !money(v.budget) ||
    !timestamp(v.requestedAt)
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
  if (!isRecord(v) || !repairReceipt(v) || !timestamp(v.approvedAt)) {
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
    !timestamp(v.completedAt)
  ) {
    return null;
  }
  return { ...v } as Completion;
}
