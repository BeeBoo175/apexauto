import { isRecord, isUuid } from "@/lib/auth/contracts";
import { isMyr } from "@/lib/currency-input";
import { fuels, transmissions } from "@/lib/vehicles/contracts";
import {
  LISTING_DESCRIPTION_MAX_LENGTH,
  MINIMUM_MANUFACTURE_YEAR,
} from "@/lib/vehicles/listing-limits";

export interface SaleVehicleReference {
  saleId: string;
  make: string;
  model: string;
  trim: string;
  manufactureYear: number;
  mileageKilometres: number;
  branchName: string;
  publishedAt: string;
  listing: {
    vehicleId: string;
    vehiclePrice: number;
    roadTax: number;
    transmission: (typeof transmissions)[number];
    fuel: (typeof fuels)[number];
    description: string;
    photoIds: string[];
  };
}

export function parseSaleVehicle(value: unknown): SaleVehicleReference | null {
  if (!isRecord(value) || !isRecord(value.listing)) {
    return null;
  }
  const listing = value.listing;
  const transmission = transmissions.find((name) => name === listing.transmission);
  const fuel = fuels.find((name) => name === listing.fuel);
  if (
    !isUuid(value.saleId) ||
    !isUuid(listing.vehicleId) ||
    typeof value.make !== "string" ||
    !value.make.trim() ||
    typeof value.model !== "string" ||
    !value.model.trim() ||
    typeof value.trim !== "string" ||
    !value.trim.trim() ||
    !Number.isSafeInteger(value.manufactureYear) ||
    Number(value.manufactureYear) < MINIMUM_MANUFACTURE_YEAR ||
    !Number.isSafeInteger(value.mileageKilometres) ||
    Number(value.mileageKilometres) < 0 ||
    typeof value.branchName !== "string" ||
    !value.branchName.trim() ||
    typeof value.publishedAt !== "string" ||
    !/(?:Z|\+00:00)$/.test(value.publishedAt) ||
    !Number.isFinite(Date.parse(value.publishedAt)) ||
    !isMyr(listing.vehiclePrice) ||
    !isMyr(listing.roadTax) ||
    !transmission ||
    !fuel ||
    typeof listing.description !== "string" ||
    !listing.description.trim() ||
    listing.description.length > LISTING_DESCRIPTION_MAX_LENGTH ||
    !Array.isArray(listing.photoIds) ||
    !listing.photoIds.every(isUuid) ||
    new Set(listing.photoIds).size !== listing.photoIds.length
  ) {
    return null;
  }
  return {
    saleId: value.saleId,
    make: value.make,
    model: value.model,
    trim: value.trim,
    manufactureYear: Number(value.manufactureYear),
    mileageKilometres: Number(value.mileageKilometres),
    branchName: value.branchName,
    publishedAt: value.publishedAt,
    listing: {
      vehicleId: listing.vehicleId,
      vehiclePrice: listing.vehiclePrice,
      roadTax: listing.roadTax,
      transmission,
      fuel,
      description: listing.description,
      photoIds: listing.photoIds,
    },
  };
}
