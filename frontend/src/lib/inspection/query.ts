import type { SearchParams } from "@/lib/query";
import { isUuid } from "@/lib/auth/contracts";
import { DEFAULT_PAGE_SIZE, isPageSize } from "@/lib/pagination";
import { inspectionStatuses, repairStatuses, type QueueFilters, type QueueKind } from "./contracts";
import {
  workshopDate,
  workshopTasks,
  workshopSorts,
  type TechnicianTask,
} from "./workshop-contracts";

export type WorkshopSection = "inspection" | "repairs" | "history";
export type RepairSelection = { requestId: string; revision: number };

export function workshopSelection(params: SearchParams) {
  const revision =
    typeof params.revision === "string" && /^\d+$/.test(params.revision)
      ? Number(params.revision)
      : NaN;
  const request =
    isUuid(params.request) && Number.isSafeInteger(revision) && revision > 0
      ? { requestId: params.request, revision }
      : null;
  const sections: WorkshopSection[] = ["inspection", "repairs", "history"];
  const section =
    sections.find((value) => value === params.section) ?? (request ? "repairs" : "inspection");
  return { id: isUuid(params.inspection) ? params.inspection : "", request, section };
}

export function legacyRepairsPath(params: SearchParams) {
  const { filters, page } = queueParams(params, "repair-requests");
  const tasks: Record<string, string> = {
    PendingApproval: "Approval",
    Approved: "Repairs",
    ApprovalNotRequired: "Repairs",
    Completed: "All",
    Superseded: "All",
    All: "All",
  };
  const query = new URLSearchParams(
    queueQuery({ ...filters, status: tasks[filters.status] ?? "All" }, page),
  );
  const selected = workshopSelection(params);
  if (selected.id) {
    query.set("inspection", selected.id);
    query.set("section", "repairs");
  }
  if (selected.request) {
    query.set("request", selected.request.requestId);
    query.set("revision", String(selected.request.revision));
  }
  return `/dashboard/inspection?${query}`;
}

export function queueParams(params: SearchParams, kind: QueueKind) {
  const get = (key: string) => (typeof params[key] === "string" ? params[key] : "");
  const options = {
    vehicles: inspectionStatuses,
    "repair-requests": repairStatuses,
    workshop: workshopTasks,
  };
  const statuses = options[kind];
  const page = Number(get("page") || 1);
  const size = Number(get("pageSize") || DEFAULT_PAGE_SIZE);
  const status =
    statuses.find((value) => value === get("status")) ?? (kind === "workshop" ? "Active" : "All");
  const defaultSort = kind === "workshop" ? workshopDate(status).column : "since";
  const defaultDirection = kind === "workshop" ? "asc" : "desc";
  const requestedDirection = get("direction");
  const requestedSort = workshopSorts.find((value) => value === get("sort"));
  const sort = kind === "workshop" && requestedSort === "since" ? defaultSort : requestedSort;
  const filters: QueueFilters = {
    search: get("search"),
    registration: get("registration"),
    vehicle: get("vehicle"),
    workStatus: get("workStatus"),
    technician: get("technician"),
    minimumBudget: get("minimumBudget"),
    maximumBudget: get("maximumBudget"),
    status,
    branchId: isUuid(get("branchId")) ? get("branchId") : "",
    from: get("from"),
    to: get("to"),
    pageSize: isPageSize(size) ? size : DEFAULT_PAGE_SIZE,
    sort: sort ?? defaultSort,
    direction:
      requestedDirection === "asc" || requestedDirection === "desc"
        ? requestedDirection
        : defaultDirection,
  };
  return { filters, page: Number.isSafeInteger(page) && page > 0 ? page : 1 };
}

export function queueQuery(filters: QueueFilters, page: number) {
  const query = new URLSearchParams({
    page: String(page),
    pageSize: String(filters.pageSize),
    status: filters.status,
    sort: filters.sort ?? "since",
    direction: filters.direction ?? "desc",
  });
  for (const key of [
    "search",
    "branchId",
    "from",
    "to",
    "workStatus",
    "technician",
    "minimumBudget",
    "maximumBudget",
    "registration",
    "vehicle",
  ] as const) {
    if (filters[key]) {
      query.set(key, filters[key]);
    }
  }
  return query.toString();
}

export function queuePath(kind: QueueKind) {
  return kind === "repair-requests" ? "/dashboard/inspection/repairs" : "/dashboard/inspection";
}

export function technicianQueueParams(params: SearchParams, task: TechnicianTask) {
  const search = params[`${task}.search`];
  return typeof search === "string" ? search.trim() : "";
}

export function technicianQueuePaging(params: SearchParams, task: TechnicianTask) {
  const page = Number(params[`${task}.page`] ?? 1);
  const pageSize = Number(params[`${task}.pageSize`] ?? DEFAULT_PAGE_SIZE);
  return {
    page: Number.isSafeInteger(page) && page > 0 && page <= 2147483647 ? page : 1,
    pageSize: isPageSize(pageSize) ? pageSize : DEFAULT_PAGE_SIZE,
  };
}

export function technicianQueueQuery(
  task: TechnicianTask,
  search: string,
  page = 1,
  pageSize = DEFAULT_PAGE_SIZE,
) {
  const query = new URLSearchParams({
    status: task,
    search,
    page: String(page),
    pageSize: String(pageSize),
  });
  return query.toString();
}

export function technicianQueueView(params: SearchParams): TechnicianTask {
  if (params.view === "claim") {
    return "Claimable";
  }
  if (params.view === "waiting") {
    return "Waiting";
  }
  return "MyWork";
}

export function queueLocationQuery(current: URLSearchParams, filters: QueueFilters, page: number) {
  const query = new URLSearchParams(queueQuery(filters, page));
  for (const name of ["inspection", "request", "revision", "section"]) {
    const value = current.get(name);
    if (value) {
      query.set(name, value);
    }
  }
  return query;
}
