import { showroomName, type Branch } from "@/lib/branches";
import type { Page } from "@/lib/api";
import { fuelLabel, type CatalogueFilters } from "./contracts";

export interface CatalogueSortOption {
  value: string;
  label: string;
  sort: CatalogueFilters["sort"];
  direction: CatalogueFilters["direction"];
}

export const CATALOGUE_SORT_OPTIONS: readonly CatalogueSortOption[] = [
  { value: "published:desc", label: "Newest listings", sort: "published", direction: "desc" },
  { value: "price:asc", label: "Price: lowest first", sort: "price", direction: "asc" },
  { value: "price:desc", label: "Price: highest first", sort: "price", direction: "desc" },
  { value: "year:desc", label: "Year: newest first", sort: "year", direction: "desc" },
  { value: "year:asc", label: "Year: oldest first", sort: "year", direction: "asc" },
  { value: "mileage:asc", label: "Mileage: lowest first", sort: "mileage", direction: "asc" },
  { value: "mileage:desc", label: "Mileage: highest first", sort: "mileage", direction: "desc" },
  { value: "published:asc", label: "Oldest listings", sort: "published", direction: "asc" },
];

export interface AppliedCataloguePill {
  key: string;
  label: string;
  without: CatalogueFilters;
}

function formatRangePrice(value: string) {
  const numeric = Number(value);
  if (!Number.isFinite(numeric)) {
    return `MYR ${value.trim()}`;
  }
  return Number.isInteger(numeric)
    ? `MYR ${numeric.toLocaleString("en-MY")}`
    : `MYR ${numeric.toLocaleString("en-MY", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
      })}`;
}

export function sortOptionValue(filters: Pick<CatalogueFilters, "sort" | "direction">) {
  const key = `${filters.sort}:${filters.direction}`;
  return CATALOGUE_SORT_OPTIONS.some((option) => option.value === key) ? key : "published:desc";
}

export function countActiveCatalogueFilters(filters: CatalogueFilters) {
  return [
    filters.make,
    filters.model,
    filters.minimumYear.trim() || filters.maximumYear.trim(),
    filters.minimumPrice.trim() || filters.maximumPrice.trim(),
    filters.transmission,
    filters.fuel,
    filters.branchId,
  ].filter((value) => value.trim().length > 0).length;
}

export function buildAppliedCataloguePills(
  filters: CatalogueFilters,
  branches: Page<Branch> | null,
): AppliedCataloguePill[] {
  const pills: AppliedCataloguePill[] = [];
  const make = filters.make.trim();
  const model = filters.model.trim();
  const minYear = filters.minimumYear.trim();
  const maxYear = filters.maximumYear.trim();
  const minPrice = filters.minimumPrice.trim();
  const maxPrice = filters.maximumPrice.trim();

  if (filters.search.trim()) {
    pills.push({
      key: "search",
      label: `Search: ${filters.search.trim()}`,
      without: { ...filters, search: "" },
    });
  }

  if (make) {
    pills.push({
      key: "make",
      label: `Make: ${make}`,
      without: { ...filters, make: "", model: "" },
    });
  }

  if (model) {
    pills.push({
      key: "model",
      label: `Model: ${model}`,
      without: { ...filters, model: "" },
    });
  }

  if (minYear || maxYear) {
    let yearLabel = `Year from ${minYear}`;
    if (minYear && maxYear) {
      yearLabel = `Year: ${minYear}–${maxYear}`;
    } else if (maxYear) {
      yearLabel = `Year up to ${maxYear}`;
    }
    pills.push({
      key: "year",
      label: yearLabel,
      without: { ...filters, minimumYear: "", maximumYear: "" },
    });
  }

  if (minPrice || maxPrice) {
    let priceLabel = `From ${formatRangePrice(minPrice)}`;
    if (minPrice && maxPrice) {
      priceLabel = `${formatRangePrice(minPrice)} – ${formatRangePrice(maxPrice)}`;
    } else if (maxPrice) {
      priceLabel = `Under ${formatRangePrice(maxPrice)}`;
    }
    pills.push({
      key: "price",
      label: priceLabel,
      without: { ...filters, minimumPrice: "", maximumPrice: "" },
    });
  }

  if (filters.transmission.trim()) {
    pills.push({
      key: "transmission",
      label: filters.transmission.trim(),
      without: { ...filters, transmission: "" },
    });
  }

  if (filters.fuel.trim()) {
    pills.push({
      key: "fuel",
      label: fuelLabel(filters.fuel.trim()),
      without: { ...filters, fuel: "" },
    });
  }

  if (filters.branchId.trim()) {
    const branch = branches?.items.find((item) => item.id === filters.branchId.trim());
    pills.push({
      key: "branch",
      label: branch ? `Showroom: ${showroomName(branch.name)}` : "Selected showroom",
      without: { ...filters, branchId: "" },
    });
  }

  return pills;
}

export type PaginationSlot = number | "ellipsis-start" | "ellipsis-end";

export function buildPaginationSlots(currentPage: number, totalPages: number): PaginationSlot[] {
  if (totalPages <= 1) {
    return [1];
  }
  if (totalPages <= 7) {
    return Array.from({ length: totalPages }, (_, index) => index + 1);
  }

  const clamped = Math.min(Math.max(1, currentPage), totalPages);
  const slots: PaginationSlot[] = [1];
  const windowStart = Math.max(2, clamped - 1);
  const windowEnd = Math.min(totalPages - 1, clamped + 1);

  if (windowStart > 2) {
    slots.push("ellipsis-start");
  }
  for (let page = windowStart; page <= windowEnd; page += 1) {
    slots.push(page);
  }
  if (windowEnd < totalPages - 1) {
    slots.push("ellipsis-end");
  }
  slots.push(totalPages);

  return slots;
}
