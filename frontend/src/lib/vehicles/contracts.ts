import type { Page } from "@/lib/api";

export const CATALOGUE_PAGE_SIZE = 12;
export const transmissions = ["Automatic", "Manual"] as const;
export const fuels = ["Petrol", "Diesel", "Hybrid", "PlugInHybrid", "Electric"] as const;
export const vehicleDetailSections = ["overview", "inspection", "financing", "trade-in"] as const;

export type VehicleDetailSection = (typeof vehicleDetailSections)[number];

export function fuelLabel(value: string) {
  return value.replace(/([a-z])([A-Z])/g, "$1 $2");
}

export function isVehicleDetailSection(value: unknown): value is VehicleDetailSection {
  return vehicleDetailSections.some((section) => section === value);
}

export interface PublicVehicle {
  id: string;
  branchId: string;
  branchName: string;
  make: string;
  model: string;
  trim: string;
  manufactureYear: number;
  mileageKilometres: number;
  vehiclePrice: number;
  transmission: (typeof transmissions)[number];
  fuel: (typeof fuels)[number];
  coverPhotoId: string | null;
  photoCount: number;
  publishedAt: string;
}

export type CataloguePage = Page<PublicVehicle>;

export interface CatalogueFilters {
  make: string;
  model: string;
  minimumYear: string;
  maximumYear: string;
  minimumPrice: string;
  maximumPrice: string;
  transmission: string;
  fuel: string;
  branchId: string;
  sort: string;
  direction: string;
}
