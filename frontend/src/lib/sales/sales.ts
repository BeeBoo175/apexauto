import { parseCountedPage } from "@/lib/api";
import type { CurrentActor } from "@/lib/auth/actor";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { isMyr } from "@/lib/currency-input";
import { DEFAULT_PAGE_SIZE, isPageSize, paginationQuery } from "@/lib/pagination";
import type { SearchParams } from "@/lib/query";
import { isSaleStatus, nullable, text, utc, type SaleStatus } from "./sale";
import { saleSection, saleVersionNumber } from "./sale-sections";

// One role-aware page: agents work from the queue, managers from the table.
export function hasSalesAccess(actor: CurrentActor) {
  return (
    actor.role === "SalesAgent" || actor.role === "BranchManager" || actor.role === "GeneralManager"
  );
}

export const isSalesManager = (actor: CurrentActor) =>
  actor.role === "BranchManager" || actor.role === "GeneralManager";

// One row in an agent's queue or a manager's table, read from the sale's current columns.
export interface SaleSummary {
  id: string;
  reservationNumber: string;
  orderNumber: string | null;
  status: SaleStatus;
  registration: string;
  manufactureYear: number;
  make: string;
  model: string;
  trim: string;
  customer: string;
  agent: string | null;
  branch: string;
  dueAt: string | null;
  pausedSeconds: number | null;
  claimedAt: string | null;
  negotiatedDiscount: number;
  discountLimitAtSubmit: number | null;
  balancePayable: number;
  submittedAt: string | null;
  decidedAt: string | null;
  closedAt: string | null;
  outcome: string | null;
  version: number;
}

const positive = (value: unknown): value is number =>
  Number.isSafeInteger(value) && Number(value) > 0;

export function parseSaleSummary(value: unknown): SaleSummary | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !text(value.reservationNumber) ||
    !nullable(text)(value.orderNumber) ||
    !isSaleStatus(value.status) ||
    !text(value.registration) ||
    !positive(value.manufactureYear) ||
    !text(value.make) ||
    !text(value.model) ||
    typeof value.trim !== "string" ||
    !text(value.customer) ||
    !nullable(text)(value.agent) ||
    !text(value.branch) ||
    !nullable(utc)(value.dueAt) ||
    !nullable(positive)(value.pausedSeconds) ||
    !nullable(utc)(value.claimedAt) ||
    !isMyr(value.negotiatedDiscount) ||
    !nullable(isMyr)(value.discountLimitAtSubmit) ||
    !isMyr(value.balancePayable) ||
    !nullable(utc)(value.submittedAt) ||
    !nullable(utc)(value.decidedAt) ||
    !nullable(utc)(value.closedAt) ||
    !nullable(text)(value.outcome) ||
    !positive(value.version)
  ) {
    return null;
  }
  return value as unknown as SaleSummary;
}

// The sale's own reference: its purchase order once submitted, before that its reservation number.
export const saleReference = (row: Pick<SaleSummary, "orderNumber" | "reservationNumber">) =>
  row.orderNumber ?? row.reservationNumber;

export type QueueView = "ToClaim" | "Handling" | "Waiting" | "Approved";

export const queueViews: readonly QueueView[] = ["Handling", "Waiting", "Approved", "ToClaim"];

export const queueViewLabels: Record<QueueView, string> = {
  Handling: "Sales in progress",
  Waiting: "Awaiting approval",
  Approved: "Approved sales",
  ToClaim: "Holds to claim",
};

export const queueViewTabs: Record<QueueView, string> = {
  Handling: "Handling",
  Waiting: "Waiting",
  Approved: "Approved",
  ToClaim: "To claim",
};

// The `view` query values, so a reloaded or shared address opens the same tab.
export const queueViewQueries: Record<QueueView, string> = {
  Handling: "handling",
  Waiting: "waiting",
  Approved: "approved",
  ToClaim: "claim",
};

const queueCountKeys = ["toClaim", "handling", "waiting", "approved"] as const;

export type QueueCounts = Record<(typeof queueCountKeys)[number], number>;

export const queueCountOf: Record<QueueView, keyof QueueCounts> = {
  ToClaim: "toClaim",
  Handling: "handling",
  Waiting: "waiting",
  Approved: "approved",
};

// The tab a sale sits in for its agent, which a search result shows beside it.
export const queueViewOf: Partial<Record<SaleStatus, QueueView>> = {
  Open: "ToClaim",
  Handling: "Handling",
  PendingApproval: "Waiting",
  Approved: "Approved",
};

export interface SalesQueue {
  items: SaleSummary[];
  total: number;
  page: number;
  pageSize: number;
  counts: QueueCounts;
}

function uniquePage<T extends { items: SaleSummary[] }>(page: T | null) {
  return page && new Set(page.items.map((item) => item.id)).size === page.items.length
    ? page
    : null;
}

export function parseSalesQueue(value: unknown): SalesQueue | null {
  const page = uniquePage(parseCountedPage(value, parseSaleSummary, queueCountKeys));
  return page?.counts ? { ...page, counts: page.counts } : null;
}

export function queueQuery(view: QueueView, search = "", page = 1) {
  const query = new URLSearchParams({
    view,
    page: String(page),
    pageSize: String(DEFAULT_PAGE_SIZE),
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

export const tableViews = [
  { value: "NeedsApproval", label: "Needs approval", count: "needsApproval" },
  { value: "Open", label: "Open", count: "open" },
  { value: "BeingHandled", label: "Being handled", count: "beingHandled" },
  { value: "Approved", label: "Approved", count: "approved" },
  { value: "History", label: "History", count: "history" },
] as const;

export type TableView = (typeof tableViews)[number]["value"];

const tableCountKeys = tableViews.map((view) => view.count);

export type TableCounts = Record<(typeof tableViews)[number]["count"], number>;

export interface SalesTablePage {
  items: SaleSummary[];
  total: number;
  page: number;
  pageSize: number;
  counts: TableCounts;
  view: TableView;
}

export function parseSalesTable(value: unknown): SalesTablePage | null {
  const page = uniquePage(parseCountedPage(value, parseSaleSummary, tableCountKeys));
  const view = isRecord(value)
    ? tableViews.find((item) => item.value === value.view)?.value
    : undefined;
  return page?.counts && view ? { ...page, counts: page.counts as TableCounts, view } : null;
}

export const tableSorts = [
  "reference",
  "registration",
  "vehicle",
  "customer",
  "agent",
  "branch",
  "status",
  "discount",
  "overLimit",
  "submitted",
  "due",
  "claimed",
  "balance",
  "decided",
  "closed",
] as const;

export const tableTextKeys = [
  "search",
  "agent",
  "branch",
  "outcome",
  "minimumDiscount",
  "maximumDiscount",
  "minimumBalance",
  "maximumBalance",
  "submittedFrom",
  "submittedTo",
  "closedFrom",
  "closedTo",
  "registration",
  "vehicle",
  "customer",
  "reference",
  "minimumOverLimit",
  "maximumOverLimit",
  "decidedFrom",
  "decidedTo",
] as const;

export interface SalesTableFilters extends Record<(typeof tableTextKeys)[number], string> {
  // Empty until the server has chosen the first tab with work.
  view: TableView | "";
  pageSize: number;
  sort: string;
  direction: "asc" | "desc";
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
  return {
    saleId: isUuid(parameters.sale) ? parameters.sale : "",
    section: saleSection(parameters.section),
    version: saleVersionNumber(parameters.version),
  };
}

// How long ago the agent took the sale, so a claim left sitting is easy to see.
export function claimAge(claimedAt: string | null, now: number) {
  if (!claimedAt) {
    return "just now";
  }
  const days = Math.floor((now - Date.parse(claimedAt)) / 86_400_000);
  if (days <= 0) {
    return "today";
  }
  return days === 1 ? "1 day ago" : `${days} days ago`;
}

export const salesTablePath = "/api/staff/sales";

export const salesQueuePath = "/api/staff/sales/queue";
