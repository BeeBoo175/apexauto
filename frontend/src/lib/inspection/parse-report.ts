import { isRecord } from "@/lib/auth/contracts";
import { ids, versions, integer, positive, text, timestamp, nullableId } from "./parse";
import type { Assignment, Reopening } from "./report-contracts";

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
