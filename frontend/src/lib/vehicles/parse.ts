import { parseItems } from "@/lib/api";
import { isNonNegativeInteger, isRecord, isUuid } from "@/lib/auth/contracts";
import { isMyr } from "@/lib/currency-input";
import {
  CATALOGUE_LISTING_PAGE_SIZE,
  CATALOGUE_PAGE_SIZE,
  fuels,
  transmissions,
  vehicleGrades,
} from "./contracts";
import type { CataloguePage, PublicVehicle } from "./contracts";
import { MINIMUM_MANUFACTURE_YEAR } from "@/lib/vehicles/listing-limits";

const text = (value: unknown): value is string =>
  typeof value === "string" && value.trim().length > 0;

function enumValue<T extends string>(values: readonly T[], value: unknown): T | null {
  return values.find((name, index) => name === value || index === value) ?? null;
}

export function parsePublicVehicle(value: unknown): PublicVehicle | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !isUuid(value.branchId) ||
    !text(value.branchName) ||
    !text(value.make) ||
    !text(value.model) ||
    !text(value.trim) ||
    !isNonNegativeInteger(value.manufactureYear) ||
    value.manufactureYear < MINIMUM_MANUFACTURE_YEAR ||
    value.manufactureYear > 9999 ||
    !isNonNegativeInteger(value.mileageKilometres) ||
    !isMyr(value.vehiclePrice) ||
    !isMyr(value.onTheRoadTotal) ||
    value.onTheRoadTotal <= 0 ||
    !isNonNegativeInteger(value.photoCount) ||
    (value.coverPhotoId !== null && !isUuid(value.coverPhotoId)) ||
    (value.photoCount === 0) !== (value.coverPhotoId === null) ||
    !text(value.publishedAt) ||
    !/(?:Z|\+00:00)$/.test(value.publishedAt) ||
    !Number.isFinite(Date.parse(value.publishedAt))
  ) {
    return null;
  }
  const transmission = enumValue(transmissions, value.transmission);
  const fuel = enumValue(fuels, value.fuel);
  if (!transmission || !fuel) {
    return null;
  }
  return {
    id: value.id,
    branchId: value.branchId,
    branchName: value.branchName,
    make: value.make,
    model: value.model,
    trim: value.trim,
    manufactureYear: value.manufactureYear,
    mileageKilometres: value.mileageKilometres,
    vehiclePrice: value.vehiclePrice,
    onTheRoadTotal: value.onTheRoadTotal,
    transmission,
    fuel,
    // The grade arrives as its name or its number; a missing or unknown one shows no grade rather than failing the card.
    grade: enumValue(vehicleGrades, value.grade),
    coverPhotoId: value.coverPhotoId,
    photoCount: value.photoCount,
    publishedAt: value.publishedAt,
  };
}

export function parseCatalogue(value: unknown): CataloguePage | null {
  if (
    !isRecord(value) ||
    !isNonNegativeInteger(value.total) ||
    !isNonNegativeInteger(value.page) ||
    value.page < 1 ||
    (value.pageSize !== CATALOGUE_PAGE_SIZE && value.pageSize !== CATALOGUE_LISTING_PAGE_SIZE)
  ) {
    return null;
  }
  const pageSize = value.pageSize;
  const items = parseItems(value.items, parsePublicVehicle);
  const remaining = Math.max(0, value.total - (value.page - 1) * pageSize);
  if (
    !items ||
    items.length !== Math.min(pageSize, remaining) ||
    new Set(items.map((item) => item.id)).size !== items.length
  ) {
    return null;
  }
  return { items, total: value.total, page: value.page, pageSize };
}
