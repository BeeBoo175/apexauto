import type { SearchParams } from "@/lib/query";
import { DEFAULT_PAGE_SIZE, isPageSize } from "@/lib/pagination";
import { historyParams, historyQuery } from "@/lib/intake/list-query";
import { intakeQueues, type StaffFilters } from "./contracts";

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
  if (filters.queue) {
    query.set("queue", filters.queue);
  }
  if (filters.handlerId) {
    query.set("handlerId", filters.handlerId);
  }
  for (const key of ["registration", "vehicle", "customer"] as const) {
    const value = filters[key]?.trim();
    if (value) {
      query.set(key, value);
    }
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

export function staffParams(params: SearchParams): { page: number; filters: StaffFilters } {
  const { filters, page } = historyParams(params);
  return {
    page,
    filters: {
      ...filters,
      status: typeof params.status === "string" ? params.status : "All",
      branchId: typeof params.branchId === "string" ? params.branchId : "",
      search: typeof params.search === "string" ? params.search : "",
      registration: typeof params.registration === "string" ? params.registration : "",
      vehicle: typeof params.vehicle === "string" ? params.vehicle : "",
      customer: typeof params.customer === "string" ? params.customer : "",
      queue: intakeQueues.find((queue) => queue === params.queue) ?? "",
      handlerId: typeof params.handlerId === "string" ? params.handlerId : "",
      sort: typeof params.sort === "string" ? params.sort : "startsAt",
      direction: params.direction === "desc" ? ("desc" as const) : ("asc" as const),
      pageSize: isPageSize(Number(params.pageSize)) ? Number(params.pageSize) : DEFAULT_PAGE_SIZE,
    },
  };
}
