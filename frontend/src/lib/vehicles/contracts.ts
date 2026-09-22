import type { Page } from "@/lib/api";

// The storefront strips and pickers read twelve cars; the catalogue page itself lists eighteen (three full rows of six).
export const CATALOGUE_PAGE_SIZE = 12;
export const CATALOGUE_LISTING_PAGE_SIZE = 18;
export const transmissions = ["Automatic", "Manual"] as const;
export const fuels = ["Petrol", "Diesel", "Hybrid", "PlugInHybrid", "Electric"] as const;
export const vehicleGrades = ["A", "B", "C"] as const;
export const vehicleDetailSections = ["overview", "inspection", "financing", "showroom"] as const;

export type VehicleDetailSection = (typeof vehicleDetailSections)[number];
export type VehicleGrade = (typeof vehicleGrades)[number];

// The grades in a catalogue filter's comma list ("A,B"), each named once and in grade order.
export function parseGradeList(value: string): VehicleGrade[] | null {
  if (!value.trim()) {
    return [];
  }
  const tokens = value.split(",").map((token) => token.trim().toUpperCase());
  const grades = vehicleGrades.filter((grade) => tokens.includes(grade));
  return tokens.length === grades.length && new Set(tokens).size === tokens.length ? grades : null;
}

export function fuelLabel(value: string) {
  if (value === "PlugInHybrid") {
    return "Plug-in hybrid";
  }
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
  onTheRoadTotal: number;
  transmission: (typeof transmissions)[number];
  fuel: (typeof fuels)[number];
  grade: VehicleGrade | null;
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
  // A comma list of inspection grades such as "A,B"; empty means every grade.
  grade: string;
  branchId: string;
  search: string;
  sort: string;
  direction: string;
}
