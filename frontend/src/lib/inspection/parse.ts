import { isRecord, isUuid } from "@/lib/auth/contracts";
import { parsePage } from "@/lib/intake/parse";
import { inspectionStatuses } from "./contracts";
import type { InspectionRow, Technician } from "./contracts";

export const text = (v: unknown): v is string => typeof v === "string" && v.length > 0;
export const integer = (v: unknown): v is number => Number.isSafeInteger(v) && Number(v) >= 0;
export const positive = (v: unknown): v is number => integer(v) && v > 0;
export const timestamp = (v: unknown): v is string =>
  text(v) && /(?:Z|\+00:00)$/.test(v) && Number.isFinite(Date.parse(v));
export const nullableId = (v: unknown) => v === null || isUuid(v);
export const ids = (v: Record<string, unknown>, keys: string[]) =>
  keys.every((key) => isUuid(v[key]));
export function enumValue<T extends string>(values: readonly T[], value: unknown): T | null {
  return values.find((name, index) => name === value || index === value) ?? null;
}

export function parseInspectionRow(v: unknown): InspectionRow | null {
  if (
    !isRecord(v) ||
    !ids(v, ["vehicleId", "branchId"]) ||
    !["branchName", "registrationPlate", "make", "model", "vehicleStatus"].every((key) =>
      text(v[key]),
    ) ||
    !timestamp(v.checkedInAt) ||
    !nullableId(v.inspectionId) ||
    !nullableId(v.technicianId) ||
    !(v.assignmentVersion === null || positive(v.assignmentVersion)) ||
    !(v.revision === null || positive(v.revision)) ||
    !enumValue(inspectionStatuses, v.status)
  ) {
    return null;
  }
  return { ...v, status: enumValue(inspectionStatuses, v.status) } as InspectionRow;
}

export function parseTechnician(v: unknown): Technician | null {
  if (!isRecord(v) || !isUuid(v.id) || !text(v.identity)) {
    return null;
  }
  return { id: v.id, identity: v.identity };
}

export const parseInspectionPage = (v: unknown) => parsePage(v, parseInspectionRow);
export const parseTechnicians = (v: unknown) => parsePage(v, parseTechnician);
