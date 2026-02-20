import { DEFAULT_PAGE_SIZE, isPageSize } from "@/lib/pagination";
import { historyParams, historyQuery } from "@/lib/intake/list-query";
import type { StaffFilters } from "./contracts";

export function staffQuery(filters: StaffFilters, page = 1) {
  const query = new URLSearchParams(historyQuery(filters, page));
  query.set("status", filters.status);
  query.set("pageSize", String(filters.pageSize));
  query.set("sort", filters.sort);
  query.set("direction", filters.direction);
  if (filters.search.trim()) {
    query.set("search", filters.search.trim());
  }
  if (filters.branchId) {
    query.set("branchId", filters.branchId);
  }
  return query.toString();
}

export function staffViewQuery(filters: StaffFilters, page = 1) {
  const query = new URLSearchParams(staffQuery(filters, page));
  query.set("view", "list");
  const defaults = {
    page: "1",
    pageSize: String(DEFAULT_PAGE_SIZE),
    status: "All",
    sort: "startsAt",
    direction: "asc",
  };
  for (const [key, value] of Object.entries(defaults)) {
    if (query.get(key) === value) {
      query.delete(key);
    }
  }
  return query.toString();
}

export function staffParams(params: Record<string, string | string[] | undefined>) {
  const { filters, page } = historyParams(params);
  return {
    page,
    filters: {
      ...filters,
      status: typeof params.status === "string" ? params.status : "All",
      branchId: typeof params.branchId === "string" ? params.branchId : "",
      search: typeof params.search === "string" ? params.search : "",
      sort: typeof params.sort === "string" ? params.sort : "startsAt",
      direction: params.direction === "desc" ? ("desc" as const) : ("asc" as const),
      pageSize: isPageSize(Number(params.pageSize)) ? Number(params.pageSize) : DEFAULT_PAGE_SIZE,
    },
  };
}
