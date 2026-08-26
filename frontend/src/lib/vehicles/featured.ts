import { formatCurrency } from "@/lib/currency-format";
import type { CatalogueFilters, PublicVehicle } from "./contracts";
import { catalogueDefaults } from "./query";

export interface FeaturedQuickFilter {
  id: string;
  label: string;
  filters: CatalogueFilters;
  showInHero?: boolean;
}

export const catalogueBudgetPresets = [
  {
    id: "under-60k",
    label: `Under ${formatCurrency(60000)}`,
    shortLabel: "< 60k",
    minimumPrice: "",
    maximumPrice: "60000",
  },
  {
    id: "60k-100k",
    label: `${formatCurrency(60000)} – ${formatCurrency(100000)}`,
    shortLabel: "60k–100k",
    minimumPrice: "60000",
    maximumPrice: "100000",
  },
  {
    id: "above-100k",
    label: `Above ${formatCurrency(100000)}`,
    shortLabel: "> 100k",
    minimumPrice: "100000",
    maximumPrice: "",
  },
] as const;

export const catalogueBudgetOptions = [
  { value: "", label: "Any budget" },
  ...catalogueBudgetPresets
    .filter((preset) => preset.maximumPrice)
    .map((preset) => ({
      value: preset.maximumPrice,
      label: `Up to ${formatCurrency(Number(preset.maximumPrice))}`,
    })),
];

export const FEATURED_QUICK_FILTERS: readonly FeaturedQuickFilter[] = [
  { id: "all", label: "All featured", filters: catalogueDefaults },
  ...catalogueBudgetPresets.map((preset): FeaturedQuickFilter => ({
    id: preset.id,
    label: preset.label,
    showInHero: preset.minimumPrice === "",
    filters: {
      ...catalogueDefaults,
      minimumPrice: preset.minimumPrice,
      maximumPrice: preset.maximumPrice,
      ...(preset.minimumPrice === "" ? { sort: "price", direction: "asc" } : {}),
    },
  })),
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
