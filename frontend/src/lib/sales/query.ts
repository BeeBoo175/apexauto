import { isUuid } from "@/lib/auth/contracts";
import { DEFAULT_PAGE_SIZE, isPageSize, paginationQuery } from "@/lib/pagination";
import type { SearchParams } from "@/lib/query";
import { isSaleVersionSection, saleSection, saleVersionNumber } from "./sale-sections";
import {
  type QueueView,
  queueViews,
  queueViewQueries,
  type QueueCounts,
  tableViews,
  tableSorts,
  tableTextKeys,
  type SalesTableFilters,
  type TableView,
} from "./contracts";

export const salePath = (id: string) => `/api/staff/sales/${id}`;

export function queueQuery(view: QueueView, search = "", page = 1, pageSize = DEFAULT_PAGE_SIZE) {
  const query = new URLSearchParams({
    view,
    page: String(page),
    pageSize: String(pageSize),
  });
  if (search.trim()) {
    query.set("search", search.trim());
  }
  return query.toString();
}

export function queueParams(parameters: SearchParams): QueueView | null {
  return queueViews.find((view) => queueViewQueries[view] === parameters.view) ?? null;
}

// The agent's queue opens on their sales in progress; with none, on the holds waiting to be claimed.
export function firstQueueView(
  requested: QueueView | null,
  counts: QueueCounts | undefined,
): QueueView {
  if (requested) {
    return requested;
  }
  return counts && counts.handling === 0 ? "ToClaim" : "Handling";
}

export function defaultTableFilters(): SalesTableFilters {
  return {
    view: "",
    pageSize: DEFAULT_PAGE_SIZE,
    sort: "",
    direction: "asc",
    ...(Object.fromEntries(tableTextKeys.map((key) => [key, ""])) as Record<
      (typeof tableTextKeys)[number],
      string
    >),
  };
}

export function tableParams(params: SearchParams) {
  const defaults = defaultTableFilters();
  const page = Number(params.page);
  const size = Number(params.pageSize);
  const filters: SalesTableFilters = {
    ...defaults,
    view: tableViews.find((view) => view.value === params.view)?.value ?? "",
    pageSize: isPageSize(size) ? size : defaults.pageSize,
    sort: tableSorts.find((sort) => sort === params.sort) ?? "",
    direction: params.direction === "desc" ? "desc" : "asc",
  };
  for (const key of tableTextKeys) {
    const value = params[key];
    if (typeof value === "string") {
      filters[key] = value;
    }
  }
  return { filters, page: Number.isSafeInteger(page) && page > 0 ? page : 1 };
}

// Tabs change only the status scope; all columns, refinements and ordering stay consistent.
export function tableViewFilters(filters: SalesTableFilters, view: TableView): SalesTableFilters {
  return {
    ...filters,
    view,
    outcome: "",
  };
}

export function tableQuery(filters: SalesTableFilters, page = 1) {
  const query = paginationQuery(page, filters.pageSize);
  if (filters.view) {
    query.set("view", filters.view);
  }
  if (filters.sort) {
    query.set("sort", filters.sort);
    query.set("direction", filters.direction);
  }
  for (const key of tableTextKeys) {
    const value = filters[key].trim();
    if (value) {
      query.set(key, value);
    }
  }
  return query.toString();
}

export function saleSelection(parameters: SearchParams) {
  const section = saleSection(parameters.section);
  return {
    saleId: isUuid(parameters.sale) ? parameters.sale : "",
    section,
    version: isSaleVersionSection(section) ? saleVersionNumber(parameters.version) : null,
  };
}

export const salesTablePath = "/api/staff/sales";

export const salesQueuePath = "/api/staff/sales/queue";
