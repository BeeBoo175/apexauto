import type { CatalogueFilters, PublicVehicle } from "./contracts";
import { catalogueDefaults } from "./query";

export interface FeaturedQuickFilter {
  id: string;
  label: string;
  filters: CatalogueFilters;
  showInHero?: boolean;
}

export const FEATURED_QUICK_FILTERS: readonly FeaturedQuickFilter[] = [
  { id: "all", label: "All featured", filters: catalogueDefaults },
  {
    id: "under-60k",
    label: "Under MYR 60,000",
    showInHero: true,
    filters: { ...catalogueDefaults, maximumPrice: "60000", sort: "price", direction: "asc" },
  },
  {
    id: "60k-100k",
    label: "MYR 60,000 – MYR 100,000",
    filters: { ...catalogueDefaults, minimumPrice: "60000", maximumPrice: "100000" },
  },
  {
    id: "above-100k",
    label: "Above MYR 100,000",
    filters: { ...catalogueDefaults, minimumPrice: "100000" },
  },
  {
    id: "automatic",
    label: "Automatic",
    showInHero: true,
    filters: { ...catalogueDefaults, transmission: "Automatic" },
  },
  {
    id: "hybrid",
    label: "Hybrid",
    showInHero: true,
    filters: { ...catalogueDefaults, fuel: "Hybrid" },
  },
];

export function selectDiverseVehicles(items: readonly PublicVehicle[], limit: number) {
  const chosen: PublicVehicle[] = [];
  const seenModels = new Set<string>();

  for (const item of items) {
    const key = `${item.make}|${item.model}`;
    if (!seenModels.has(key)) {
      seenModels.add(key);
      chosen.push(item);
      if (chosen.length >= limit) {
        return chosen;
      }
    }
  }

  for (const item of items) {
    if (!chosen.some((existing) => existing.id === item.id)) {
      chosen.push(item);
      if (chosen.length >= limit) {
        break;
      }
    }
  }

  return chosen;
}

export function matchingQuickFilterId(filters: CatalogueFilters): string | null {
  const match = FEATURED_QUICK_FILTERS.find(
    (chip) =>
      chip.filters.maximumPrice === filters.maximumPrice &&
      chip.filters.minimumPrice === filters.minimumPrice &&
      chip.filters.transmission === filters.transmission &&
      chip.filters.fuel === filters.fuel &&
      chip.filters.make === filters.make &&
      chip.filters.model === filters.model &&
      chip.filters.branchId === filters.branchId &&
      chip.filters.minimumYear === filters.minimumYear &&
      chip.filters.maximumYear === filters.maximumYear &&
      chip.filters.search === filters.search &&
      (chip.id !== "all" ||
        (chip.filters.sort === filters.sort && chip.filters.direction === filters.direction)),
  );
  return match?.id ?? null;
}
