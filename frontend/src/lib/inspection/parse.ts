import {
  isNonNegativeInteger,
  isPositiveInteger,
  isRecord,
  isUtcInstant,
  isUuid,
} from "@/lib/auth/contracts";
import { parseItems, parsePage } from "@/lib/api";
import { inspectionStatuses, repairStatuses, severities } from "./contracts";
import type {
  Assessment,
  Draft,
  Entry,
  InspectionRow,
  Point,
  RepairRow,
  Save,
  Technician,
  PhotoReference,
} from "./contracts";

export const text = (v: unknown): v is string => typeof v === "string" && v.length > 0;
export const money = (v: unknown): v is number =>
  typeof v === "number" &&
  Number.isFinite(v) &&
  v >= 0 &&
  Number.isSafeInteger(Math.round(v * 100));
export const nullableId = (v: unknown) => v === null || isUuid(v);
export const nullableTime = (v: unknown) => v === null || isUtcInstant(v);
export const ids = (v: Record<string, unknown>, keys: string[]) =>
  keys.every((key) => isUuid(v[key]));
export const versions = (v: Record<string, unknown>, keys: string[]) =>
  keys.every((key) => isPositiveInteger(v[key]));
export function enumValue<T extends string>(values: readonly T[], value: unknown): T | null {
  return values.find((name, index) => name === value || index === value) ?? null;
}

export function parseAssessment(v: unknown): Assessment | null {
  if (!isRecord(v) || !money(v.estimatedRepairCost) || typeof v.isRejected !== "boolean") {
    return null;
  }
  const grade = enumValue(["A", "B", "C"] as const, v.grade);
  if ((v.grade !== null && !grade) || v.isRejected !== (grade === null)) {
    return null;
  }
  return { estimatedRepairCost: v.estimatedRepairCost, grade, isRejected: v.isRejected };
}

export function parsePoint(v: unknown): Point | null {
  if (!isRecord(v) || !text(v.code) || !text(v.description)) {
    return null;
  }
  const category = enumValue(
    [
      "FrameChassis",
      "EngineTransmission",
      "ExteriorPaint",
      "InteriorElectrical",
      "UnderbodyBrakesSuspension",
    ],
    v.category,
  );
  return category ? { code: v.code, category, description: v.description } : null;
}

export function parseEntry(v: unknown): Entry | null {
  if (!isRecord(v) || !text(v.pointCode) || !text(v.notes) || !money(v.estimatedRepairCost)) {
    return null;
  }
  const severity = enumValue(severities, v.severity);
  const kind = enumValue(["Finding", "NotFitted"] as const, v.kind);
  if (!kind) {
    return null;
  }
  if (
    (kind === "Finding" && !severity) ||
    (kind === "NotFitted" && (v.severity !== null || v.estimatedRepairCost !== 0))
  ) {
    return null;
  }
  return {
    pointCode: v.pointCode,
    kind,
    severity,
    estimatedRepairCost: v.estimatedRepairCost,
    notes: v.notes,
  };
}

export function parseSave(v: unknown): Save | null {
  if (
    !isRecord(v) ||
    !ids(v, ["operationId", "inspectionId", "actorId"]) ||
    !versions(v, ["assignmentVersion", "version"]) ||
    !isNonNegativeInteger(v.expectedVersion) ||
    !text(v.catalogVersion) ||
    !isUtcInstant(v.recordedAt)
  ) {
    return null;
  }
  const entries = parseItems(v.entries, parseEntry);
  const assessment = parseAssessment(v.assessment);
  const photoIds = parseItems(v.photoIds, (id) => (isUuid(id) ? id : null));
  const examined =
    v.examinedPointCodes === null
      ? null
      : parseItems(v.examinedPointCodes, (code) => (text(code) ? code : null));
  if (!entries || !assessment || !photoIds || (v.examinedPointCodes !== null && !examined)) {
    return null;
  }
  return { ...v, entries, assessment, photoIds, examinedPointCodes: examined } as Save;
}

export function parseDraft(v: unknown): Draft | null {
  if (
    !isRecord(v) ||
    !ids(v, ["id", "vehicleId", "branchId", "technicianId"]) ||
    !text(v.technicianIdentity) ||
    !versions(v, ["assignmentVersion", "revision"]) ||
    !isNonNegativeInteger(v.repairVersion) ||
    !isNonNegativeInteger(v.reopenedAfterSaveVersion) ||
    typeof v.isSigned !== "boolean" ||
    !isUtcInstant(v.createdAt) ||
    !text(v.catalogVersion)
  ) {
    return null;
  }
  const catalog = parseItems(v.catalog, parsePoint);
  const latestSave = v.latestSave === null ? null : parseSave(v.latestSave);
  const photoReferences = parseItems(v.photoReferences, parsePhotoReference);
  const savedIds = new Set(latestSave?.photoIds ?? []);
  const vehicle = v.vehicle;
  if (
    !photoReferences ||
    photoReferences.length !== savedIds.size ||
    new Set(photoReferences.map((p) => p.id)).size !== savedIds.size ||
    photoReferences.some(
      (p) =>
        !savedIds.has(p.id) ||
        !latestSave?.entries.some((e) => e.kind === "Finding" && e.pointCode === p.pointCode),
    ) ||
    !catalog ||
    !catalog.length ||
    new Set(catalog.map((p) => p.code)).size !== catalog.length ||
    (v.latestSave !== null && !latestSave) ||
    !isRecord(vehicle) ||
    !["registrationPlate", "make", "model", "vin", "status", "branchName"].every((key) =>
      text(vehicle[key]),
    ) ||
    !isNonNegativeInteger(vehicle.mileageKilometres)
  ) {
    return null;
  }
  return { ...v, catalog, latestSave, vehicle, photoReferences } as Draft;
}

export function parsePhotoReference(v: unknown): PhotoReference | null {
  if (!isRecord(v) || !isUuid(v.id) || !text(v.pointCode)) {
    return null;
  }
  return { id: v.id, pointCode: v.pointCode };
}

function parseInspectionRow(v: unknown): InspectionRow | null {
  if (
    !isRecord(v) ||
    !ids(v, ["vehicleId", "branchId"]) ||
    !["branchName", "registrationPlate", "make", "model", "vehicleStatus"].every((key) =>
      text(v[key]),
    ) ||
    !isUtcInstant(v.checkedInAt) ||
    !nullableId(v.inspectionId) ||
    !nullableId(v.technicianId) ||
    !(v.assignmentVersion === null || isPositiveInteger(v.assignmentVersion)) ||
    !(v.revision === null || isPositiveInteger(v.revision)) ||
    !enumValue(inspectionStatuses, v.status)
  ) {
    return null;
  }
  return { ...v, status: enumValue(inspectionStatuses, v.status) } as InspectionRow;
}

function parseRepairRow(v: unknown): RepairRow | null {
  if (
    !isRecord(v) ||
    !ids(v, ["requestId", "inspectionId", "vehicleId", "branchId", "technicianId"]) ||
    !versions(v, ["assignmentVersion", "revision", "repairVersion"]) ||
    !text(v.branchName) ||
    !text(v.registrationPlate) ||
    !money(v.budget) ||
    !isUtcInstant(v.requestedAt) ||
    !nullableId(v.approvalId) ||
    !nullableId(v.coveringApprovalId) ||
    !nullableId(v.completionId) ||
    !enumValue(repairStatuses, v.status)
  ) {
    return null;
  }
  return { ...v, status: enumValue(repairStatuses, v.status) } as RepairRow;
}

function parseTechnician(v: unknown): Technician | null {
  if (!isRecord(v) || !isUuid(v.id) || !text(v.identity)) {
    return null;
  }
  return { id: v.id, identity: v.identity };
}

export const parseInspectionPage = (v: unknown) => parsePage(v, parseInspectionRow);
export const parseRepairPage = (v: unknown) => parsePage(v, parseRepairRow);
export const parseTechnicians = (v: unknown) => parsePage(v, parseTechnician);
