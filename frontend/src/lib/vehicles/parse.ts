import { parseItems } from "@/lib/api";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { CATALOGUE_PAGE_SIZE, fuels, transmissions } from "./contracts";
import type { CataloguePage, PublicVehicle } from "./contracts";

const text = (value: unknown): value is string =>
  typeof value === "string" && value.trim().length > 0;
const integer = (value: unknown): value is number =>
  Number.isSafeInteger(value) && Number(value) >= 0;

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
    !integer(value.manufactureYear) ||
    value.manufactureYear < 1886 ||
    value.manufactureYear > 9999 ||
    !integer(value.mileageKilometres) ||
    typeof value.vehiclePrice !== "number" ||
    !Number.isFinite(value.vehiclePrice) ||
    value.vehiclePrice < 0 ||
    !Number.isSafeInteger(Math.round(value.vehiclePrice * 100)) ||
    !integer(value.photoCount) ||
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
    transmission,
    fuel,
    coverPhotoId: value.coverPhotoId,
    photoCount: value.photoCount,
    publishedAt: value.publishedAt,
  };
}

export function parseCatalogue(value: unknown): CataloguePage | null {
  if (
    !isRecord(value) ||
    !integer(value.total) ||
    !integer(value.page) ||
    value.page < 1 ||
    value.pageSize !== CATALOGUE_PAGE_SIZE
  ) {
    return null;
  }
  const items = parseItems(value.items, parsePublicVehicle);
  const remaining = Math.max(0, value.total - (value.page - 1) * CATALOGUE_PAGE_SIZE);
  if (
    !items ||
    items.length !== Math.min(CATALOGUE_PAGE_SIZE, remaining) ||
    new Set(items.map((item) => item.id)).size !== items.length
  ) {
    return null;
  }
  return { items, total: value.total, page: value.page, pageSize: CATALOGUE_PAGE_SIZE };
}
