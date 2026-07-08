import type { AppliedFilter } from "@/lib/staff/filter-summary";
import type { TableSort } from "@/components/ui/data-table/types";

// What the team table is showing beyond the period and branch: a search, a name filter, a status, minimum counts per
// measure and one sort. The charts and headline figures ignore all of it and always cover everyone.
export interface TeamFilters {
  search: string;
  name: string;
  status: string;
  // Measure id to the least count a person must have, such as { checkIns: 5 }.
  minimums: Record<string, number>;
  sort: TableSort | null;
}

export const noTeamFilters: TeamFilters = {
  search: "",
  name: "",
  status: "",
  minimums: {},
  sort: null,
};

export const statusChoices = [
  { value: "", label: "All statuses" },
  { value: "Active", label: "Active" },
  { value: "Transferred", label: "Transferred" },
  { value: "Deactivated", label: "Deactivated" },
];

// The query parameters the Team reads understand, left out when unset so the default order stays the default.
export function teamQuery(filters: TeamFilters) {
  const query = new URLSearchParams();
  if (filters.search) {
    query.set("search", filters.search);
  }
  if (filters.name) {
    query.set("name", filters.name);
  }
  if (filters.status) {
    query.set("status", filters.status);
  }
  const minimums = Object.entries(filters.minimums)
    .map(([key, count]) => `${key}:${count}`)
    .join(",");
  if (minimums) {
    query.set("min", minimums);
  }
  if (filters.sort) {
    query.set("sort", filters.sort.column);
    query.set("direction", filters.sort.direction);
  }
  return query.toString();
}

export function teamFiltersApplied(filters: TeamFilters) {
  return Boolean(
    filters.search || filters.name || filters.status || Object.keys(filters.minimums).length > 0,
  );
}

// The removable pills under the search: one for each filter that applies.
export function appliedTeamFilters(
  filters: TeamFilters,
  labelOf: (measure: string) => string,
): AppliedFilter<TeamFilters>[] {
  const applied: AppliedFilter<TeamFilters>[] = [];
  if (filters.search) {
    applied.push({
      key: "term",
      label: `Search: ${filters.search}`,
      without: { ...filters, search: "" },
    });
  }
  if (filters.name) {
    applied.push({
      key: "name",
      label: `Name: ${filters.name}`,
      without: { ...filters, name: "" },
    });
  }
  if (filters.status) {
    applied.push({ key: "status", label: filters.status, without: { ...filters, status: "" } });
  }
  for (const [measure, count] of Object.entries(filters.minimums)) {
    const { [measure]: removed, ...rest } = filters.minimums;
    void removed;
    applied.push({
      key: "minimum",
      label: `${labelOf(measure)}: at least ${count}`,
      without: { ...filters, minimums: rest },
    });
  }
  return applied;
}
