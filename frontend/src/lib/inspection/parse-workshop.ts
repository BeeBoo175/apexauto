import { isRecord, isUuid } from "@/lib/auth/contracts";
import { parsePage } from "@/lib/api";
import { enumValue, money, nullableId, positive, text, timestamp } from "./parse";
import { workshopStatuses, type WorkshopRow } from "./workshop-contracts";

function parseWorkshopRow(value: unknown): WorkshopRow | null {
  if (
    !isRecord(value) ||
    !isUuid(value.vehicleId) ||
    !isUuid(value.branchId) ||
    !["branchName", "registrationPlate", "make", "model", "vehicleStatus"].every((key) =>
      text(value[key]),
    ) ||
    !nullableId(value.inspectionId) ||
    !nullableId(value.technicianId) ||
    !(value.technicianIdentity === null || text(value.technicianIdentity)) ||
    !(value.revision === null || positive(value.revision)) ||
    !nullableId(value.requestId) ||
    !(value.budget === null || money(value.budget)) ||
    !timestamp(value.since) ||
    (value.dateSource !== "CheckedIn" && value.dateSource !== "Requested")
  ) {
    return null;
  }
  const status = enumValue(workshopStatuses, value.status);
  if (
    !status ||
    (value.requestId === null) !== (value.budget === null) ||
    (value.requestId === null) !== (value.dateSource === "CheckedIn")
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

export const parseWorkshopPage = (value: unknown) => parsePage(value, parseWorkshopRow);
