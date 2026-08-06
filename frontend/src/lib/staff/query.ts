import type { SearchParams } from "@/lib/query";
import { isUuid } from "@/lib/auth/contracts";
import { DEFAULT_PAGE_SIZE, isPageSize } from "@/lib/pagination";
import { historyParams, historyQuery } from "@/lib/intake/list-query";
import {
  intakeQueues,
  worklistStatuses,
  type AppointmentDetail,
  type IntakeQueue,
  type StaffFilters,
} from "./contracts";

export function parseIntakeQueue(value: unknown): IntakeQueue | "" {
  return intakeQueues.find((queue) => queue === value) ?? "";
}

function normalizeWorklistStatus(value: unknown): string {
  return worklistStatuses.find((status) => status === value) ?? "all";
}

export function appointmentQueue(detail: AppointmentDetail): IntakeQueue {
  if (detail.status !== "Booked") {
    return "history";
  }
  return detail.ownership?.handlerId ? "myWork" : "toClaim";
}

export function intakeQueueParams(params: SearchParams): { page: number; filters: StaffFilters } {
  const { filters, page } = staffParams({
    view: params.view,
    search: params.search,
    page: params.page,
    pageSize: params.pageSize,
  });
  return { page, filters: { ...filters, queue: filters.queue || "myWork" } };
}

// Managers use the status views only, so an agent queue token falls back to the default view.
export function managerQueueParams(params: SearchParams): { page: number; filters: StaffFilters } {
  const { filters, page } = staffParams(params);
  return { page, filters: { ...filters, queue: "" } };
}

export function staffQuery(filters: StaffFilters, page = 1) {
  const query = new URLSearchParams(historyQuery(filters, page));
  // One view token: the agent queue when set, otherwise the manager's status tab.
  query.set("view", filters.queue || filters.status);
  query.set("pageSize", String(filters.pageSize));
  query.set("sort", filters.sort);
  query.set("direction", filters.direction);
  if (filters.search.trim()) {
    query.set("search", filters.search.trim());
  }
  if (filters.branchId) {
    query.set("branchId", filters.branchId);
  }
  if (filters.handlerId) {
    query.set("handlerId", filters.handlerId);
  }
  for (const key of ["registration", "vehicle", "customer", "handler"] as const) {
    const value = filters[key]?.trim();
    if (value) {
      query.set(key, value);
    }
  }
  return query.toString();
}

export function staffViewQuery(filters: StaffFilters, page = 1) {
  const query = new URLSearchParams(staffQuery(filters, page));
  const defaults = {
    page: "1",
    pageSize: String(DEFAULT_PAGE_SIZE),
    view: filters.queue ? "myWork" : "all",
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
      status: normalizeWorklistStatus(params.view),
      branchId: typeof params.branchId === "string" ? params.branchId : "",
      search: typeof params.search === "string" ? params.search : "",
      registration: typeof params.registration === "string" ? params.registration : "",
      vehicle: typeof params.vehicle === "string" ? params.vehicle : "",
      customer: typeof params.customer === "string" ? params.customer : "",
      queue: parseIntakeQueue(params.view),
      handlerId:
        typeof params.handlerId === "string" && isUuid(params.handlerId) ? params.handlerId : "",
      handler: typeof params.handler === "string" ? params.handler : "",
      sort: typeof params.sort === "string" ? params.sort : "startsAt",
      direction: params.direction === "desc" ? ("desc" as const) : ("asc" as const),
      pageSize: isPageSize(Number(params.pageSize)) ? Number(params.pageSize) : DEFAULT_PAGE_SIZE,
    },
  };
}
