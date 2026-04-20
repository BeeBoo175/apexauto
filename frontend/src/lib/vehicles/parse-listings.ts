import { parsePage } from "@/lib/api";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { isMyr } from "@/lib/currency-input";
import { vehicleStatuses, type ListingItem } from "./listings";

const text = (value: unknown): value is string =>
  typeof value === "string" && value.trim().length > 0;
const integer = (value: unknown): value is number =>
  Number.isSafeInteger(value) && Number(value) >= 0;
const nullableId = (value: unknown) => value === null || isUuid(value);
const nullableTimestamp = (value: unknown): value is string | null =>
  value === null ||
  (typeof value === "string" && /(?:Z|\+00:00)$/.test(value) && Number.isFinite(Date.parse(value)));

export function parseListing(value: unknown): ListingItem | null {
  if (
    !isRecord(value) ||
    !isUuid(value.vehicleId) ||
    !isUuid(value.branchId) ||
    !text(value.branchName) ||
    !text(value.registrationPlate) ||
    !text(value.make) ||
    !text(value.model) ||
    !text(value.trim) ||
    !integer(value.manufactureYear) ||
    !integer(value.mileageKilometres) ||
    !integer(value.version) ||
    !nullableId(value.revisionId) ||
    !nullableId(value.publicationId) ||
    !nullableId(value.publishedRevisionId) ||
    typeof value.isWithdrawn !== "boolean" ||
    typeof value.isPublic !== "boolean" ||
    typeof value.isReserved !== "boolean" ||
    typeof value.hasCurrentPublicationInspection !== "boolean" ||
    !nullableTimestamp(value.savedAt) ||
    !nullableTimestamp(value.approvedAt) ||
    !nullableTimestamp(value.withdrawnAt)
  ) {
    return null;
  }
  const vehicleStatus = vehicleStatuses.find(
    (status, index) => status === value.vehicleStatus || index === value.vehicleStatus,
  );
  if (!vehicleStatus) {
    return null;
  }
  const price = value.vehiclePrice;
  if (price !== null && !isMyr(price)) {
    return null;
  }
  const approvedPrice = value.approvedVehiclePrice;
  if (approvedPrice !== null && !isMyr(approvedPrice)) {
    return null;
  }
  return {
    vehicleId: value.vehicleId,
    branchId: value.branchId,
    branchName: value.branchName,
    registrationPlate: value.registrationPlate,
    make: value.make,
    model: value.model,
    trim: value.trim,
    manufactureYear: value.manufactureYear,
    mileageKilometres: value.mileageKilometres,
    vehicleStatus,
    revisionId: value.revisionId as string | null,
    version: value.version,
    vehiclePrice: price,
    publicationId: value.publicationId as string | null,
    publishedRevisionId: value.publishedRevisionId as string | null,
    isWithdrawn: value.isWithdrawn,
    isPublic: value.isPublic,
    approvedVehiclePrice: approvedPrice,
    savedAt: value.savedAt,
    approvedAt: value.approvedAt,
    withdrawnAt: value.withdrawnAt,
    isReserved: value.isReserved,
    hasCurrentPublicationInspection: value.hasCurrentPublicationInspection,
  };
}

export function parseListings(value: unknown) {
  const page = parsePage(value, parseListing);
  if (!page || new Set(page.items.map((item) => item.vehicleId)).size !== page.items.length) {
    return null;
  }
  return page;
}
