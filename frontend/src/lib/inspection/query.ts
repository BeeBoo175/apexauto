import type { SearchParams } from "@/lib/query";
import { isUuid } from "@/lib/auth/contracts";
import { DEFAULT_PAGE_SIZE, isPageSize } from "@/lib/pagination";
import {
  checklistCategories,
  inspectionStatuses,
  repairStatuses,
  type QueueFilters,
  type QueueKind,
} from "./contracts";
import {
  workshopDate,
  workshopTasks,
  workshopSorts,
  workshopStatuses,
  type TechnicianTask,
} from "./workshop-contracts";

export type WorkshopSection = "inspection" | "repairs" | "history";
export type RepairSelection = { requestId: string; revision: number };

export function inspectionQueryToken(value: string) {
  return value.replace(/^[A-Z]/, (letter) => letter.toLowerCase());
}

// A checklist category travels as its camelCase token; anything unknown means the default first category.
export function checklistCategoryToken(value: unknown) {
  const category = checklistCategories.find(
    (candidate) => inspectionQueryToken(candidate) === value,
  );
  return category ? inspectionQueryToken(category) : "";
}

function queueToken<T extends string>(values: readonly T[], value: string) {
  return values.find((candidate) => inspectionQueryToken(candidate) === value);
}

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
  return {
    id: isUuid(params.inspection) ? params.inspection : "",
    request,
    section,
    category: checklistCategoryToken(params.category),
  };
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
  const status = queueToken(statuses, get("view")) ?? (kind === "workshop" ? "Active" : "All");
  const defaultSort = kind === "workshop" ? workshopDate(status).column : "since";
  const defaultDirection = kind === "workshop" ? "asc" : "desc";
  const requestedDirection = get("direction");
  const requestedSort = workshopSorts.find((value) => value === get("sort"));
  const sort = kind === "workshop" && requestedSort === "since" ? defaultSort : requestedSort;
  const filters: QueueFilters = {
    search: get("search"),
    registration: get("registration"),
    vehicle: get("vehicle"),
    workStatus: queueToken(workshopStatuses, get("workStatus")) ?? "",
    handler: get("handler"),
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
    view: inspectionQueryToken(filters.status),
    sort: filters.sort ?? "since",
    direction: filters.direction ?? "desc",
  });
  for (const key of [
    "search",
    "branchId",
    "from",
    "to",
    "workStatus",
    "handler",
    "minimumBudget",
    "maximumBudget",
    "registration",
    "vehicle",
  ] as const) {
    if (filters[key]) {
      query.set(key, key === "workStatus" ? inspectionQueryToken(filters[key]) : filters[key]);
    }
  }
  return query.toString();
}

export function technicianQueueQuery(
  task: TechnicianTask,
  search: string,
  page = 1,
  pageSize = DEFAULT_PAGE_SIZE,
) {
  const query = new URLSearchParams({
    view: inspectionQueryToken(task),
    search,
    page: String(page),
    pageSize: String(pageSize),
  });
  return query.toString();
}
