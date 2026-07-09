import type { SearchParams } from "@/lib/query";
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

export interface TeamUrlState {
  filters: TeamFilters;
  page: number;
  pageSize: number;
  // The open person's drawer, as personId:branchId, or empty.
  person: string;
}

const pageSizes = [10, 25, 50];
const personPattern = /^[0-9a-f-]{36}:[0-9a-f-]{36}$/i;

export const defaultTeamUrl: TeamUrlState = {
  filters: noTeamFilters,
  page: 1,
  pageSize: 25,
  person: "",
};

const first = (value: string | string[] | undefined) =>
  (Array.isArray(value) ? value[0] : value) ?? "";

// What the address says about a team table, read the way the other staff pages read theirs: unknown values fall
// back to the default.
export function parseTeamUrl(parameters: SearchParams): TeamUrlState {
  const minimums: Record<string, number> = {};
  for (const pair of first(parameters.min).split(",")) {
    const [key, count] = pair.split(":");
    const value = Number.parseInt(count ?? "", 10);
    if (/^\w{1,40}$/.test(key ?? "") && Number.isInteger(value) && value >= 0) {
      minimums[key] = value;
    }
  }
  const sort = first(parameters.sort);
  const status = first(parameters.status);
  const page = Number.parseInt(first(parameters.page), 10);
  const pageSize = Number.parseInt(first(parameters.pageSize), 10);
  const person = first(parameters.person);
  return {
    filters: {
      search: first(parameters.search).trim().slice(0, 100),
      name: first(parameters.name).trim().slice(0, 100),
      status: statusChoices.some((choice) => choice.value === status) ? status : "",
      minimums,
      sort: /^\w{1,40}$/.test(sort)
        ? { column: sort, direction: first(parameters.direction) === "desc" ? "desc" : "asc" }
        : null,
    },
    page: Number.isSafeInteger(page) && page > 0 ? page : 1,
    pageSize: pageSizes.includes(pageSize) ? pageSize : 25,
    person: personPattern.test(person) ? person : "",
  };
}

// Every address parameter a team table owns. The default for each is empty, so it leaves the address.
export const teamUrlKeys = [
  "search",
  "name",
  "status",
  "min",
  "sort",
  "direction",
  "page",
  "pageSize",
  "person",
  "section",
];

export function teamUrlValues({ filters, page, pageSize, person }: TeamUrlState) {
  const query = new URLSearchParams(teamQuery(filters));
  return {
    search: query.get("search") ?? "",
    name: query.get("name") ?? "",
    status: query.get("status") ?? "",
    min: query.get("min") ?? "",
    sort: query.get("sort") ?? "",
    direction: filters.sort?.direction === "desc" ? "desc" : "",
    page: page > 1 ? String(page) : "",
    pageSize: pageSize === 25 ? "" : String(pageSize),
    person,
    // The drawer's tab belongs to the open drawer.
    ...(person ? {} : { section: "" }),
  };
}
