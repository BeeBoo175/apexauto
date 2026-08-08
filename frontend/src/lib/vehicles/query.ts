import type { SearchParams } from "@/lib/query";
import { isUuid } from "@/lib/auth/contracts";
import { parseMyr } from "@/lib/currency-input";
import { fuels, isVehicleDetailSection, transmissions, type CatalogueFilters } from "./contracts";
import { MINIMUM_MANUFACTURE_YEAR } from "@/lib/vehicles/listing-limits";

export const catalogueDefaults: CatalogueFilters = {
  make: "",
  model: "",
  minimumYear: "",
  maximumYear: "",
  minimumPrice: "",
  maximumPrice: "",
  transmission: "",
  fuel: "",
  branchId: "",
  sort: "published",
  direction: "desc",
};

export function vehicleDetailSectionHref(id: string, params: SearchParams): string | null {
  if (!isVehicleDetailSection(params.section)) {
    return null;
  }
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (key === "section" || value === undefined) {
      continue;
    }
    const values = Array.isArray(value) ? value : [value];
    values.forEach((entry) => query.append(key, entry));
  }
  const suffix = query.size > 0 ? `?${query}` : "";
  return `/vehicles/${encodeURIComponent(id)}${suffix}#${params.section}`;
}

export function catalogueParams(params: SearchParams) {
  const filters = { ...catalogueDefaults };
  for (const key of Object.keys(filters) as (keyof CatalogueFilters)[]) {
    if (typeof params[key] === "string") {
      filters[key] = params[key];
    }
  }
  const page = Number(params.page ?? 1);
  return { filters, page: Number.isSafeInteger(page) && page > 0 ? page : 1 };
}

export function catalogueQuery(filters: CatalogueFilters, page = 1) {
  const query = new URLSearchParams({ page: String(page) });
  for (const [key, value] of Object.entries(filters)) {
    if (value.trim()) {
      query.set(key, value.trim());
    }
  }
  return query.toString();
}

export function catalogueHref(filters: CatalogueFilters, page = 1) {
  const isDefaultFilters = (Object.keys(catalogueDefaults) as (keyof CatalogueFilters)[]).every(
    (key) => filters[key].trim() === catalogueDefaults[key],
  );
  if (page === 1 && isDefaultFilters) {
    return "/vehicles";
  }
  return `/vehicles?${catalogueQuery(filters, page)}`;
}

export function catalogueFilterError(filters: CatalogueFilters): string | null {
  if (
    [filters.make, filters.model].some((value) => value.trim().length > 100 || value.includes("\0"))
  ) {
    return "Keep make and model to 100 characters each.";
  }
  const years = [filters.minimumYear, filters.maximumYear];
  if (
    years.some(
      (value) => value && (!/^\d{4}$/.test(value) || Number(value) < MINIMUM_MANUFACTURE_YEAR),
    )
  ) {
    return `Enter years between ${MINIMUM_MANUFACTURE_YEAR} and 9999.`;
  }
  if (years.every(Boolean) && Number(years[0]) > Number(years[1])) {
    return "Minimum year must not exceed maximum year.";
  }
  const prices = [filters.minimumPrice, filters.maximumPrice];
  if (prices.some((value) => value && parseMyr(value) === null)) {
    return "Enter prices in MYR with at most two decimal places.";
  }
  if (prices.every(Boolean) && Number(prices[0]) > Number(prices[1])) {
    return "Minimum price must not exceed maximum price.";
  }
  if (
    (filters.transmission && !transmissions.some((value) => value === filters.transmission)) ||
    (filters.fuel && !fuels.some((value) => value === filters.fuel)) ||
    (filters.branchId && !isUuid(filters.branchId)) ||
    !["published", "price", "year", "mileage"].includes(filters.sort) ||
    !["asc", "desc"].includes(filters.direction)
  ) {
    return "Choose valid catalogue filters and ordering.";
  }
  return null;
}
