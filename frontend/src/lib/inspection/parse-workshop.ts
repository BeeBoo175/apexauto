import { isPositiveInteger, isRecord, isUtcInstant, isUuid } from "@/lib/auth/contracts";
import { parseCountedPage, parseItems } from "@/lib/api";
import { isPageSize } from "@/lib/pagination";
import { enumValue, money, nullableId, text } from "./parse";
import { workshopStatuses, type WorkshopJobs, type WorkshopRow } from "./workshop-contracts";

function parseWorkshopRow(value: unknown): WorkshopRow | null {
  if (
    !isRecord(value) ||
    !isUuid(value.vehicleId) ||
    !isUuid(value.branchId) ||
    !["branchName", "registrationPlate", "make", "model", "trim", "vehicleStatus"].every((key) =>
      text(value[key]),
    ) ||
    !isPositiveInteger(value.manufactureYear) ||
    !isUtcInstant(value.checkedInAt) ||
    !(value.requestedAt === null || isUtcInstant(value.requestedAt)) ||
    !nullableId(value.inspectionId) ||
    !nullableId(value.technicianId) ||
    !(value.technicianIdentity === null || text(value.technicianIdentity)) ||
    !(value.revision === null || isPositiveInteger(value.revision)) ||
    !nullableId(value.requestId) ||
    !(value.budget === null || money(value.budget)) ||
    !isUtcInstant(value.since) ||
    (value.dateSource !== "CheckedIn" && value.dateSource !== "Requested")
  ) {
    return null;
  }
  const status = enumValue(workshopStatuses, value.status);
  if (
    !status ||
    (value.requestId === null) !== (value.budget === null) ||
    (value.requestId === null) !== (value.dateSource === "CheckedIn") ||
    (value.requestId === null) !== (value.requestedAt === null)
  ) {
    return null;
  }
  if (
    (value.inspectionId === null) !== (value.technicianId === null) ||
    (value.requestId !== null && (value.inspectionId === null || value.revision === null))
  ) {
    return null;
  }
  return { ...value, status } as WorkshopRow;
}

const workshopCountKeys = ["active", "inspection", "approval", "repairs", "all"] as const;

export const parseWorkshopPage = (value: unknown) =>
  parseCountedPage(value, parseWorkshopRow, workshopCountKeys);

const cursor = (value: unknown) =>
  value === null || (typeof value === "string" && value.length > 0 && value.length <= 2048);

export function parseWorkshopJobs(value: unknown): WorkshopJobs | null {
  if (
    !isRecord(value) ||
    !Number.isSafeInteger(value.total) ||
    Number(value.total) < 0 ||
    !isPageSize(value.pageSize) ||
    !cursor(value.after) ||
    !cursor(value.nextCursor)
  ) {
    return null;
  }
  const items = parseItems(value.items, parseWorkshopRow);
  if (
    !items ||
    items.length > value.pageSize ||
    items.length > Number(value.total) ||
    new Set(items.map((item) => item.vehicleId)).size !== items.length ||
    (value.nextCursor !== null && items.length !== value.pageSize)
  ) {
    return null;
  }
  return { ...value, items } as WorkshopJobs;
}
