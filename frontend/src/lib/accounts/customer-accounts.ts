import { parseActor, type CurrentActor } from "@/lib/auth/actor";
import { isNonNegativeInteger, isRecord, isUuid } from "@/lib/auth/contracts";
import { parsePage } from "@/lib/api";
import { DEFAULT_PAGE_SIZE, isPageSize, paginationQuery } from "@/lib/pagination";
import type { SearchParams } from "@/lib/query";

export const customerAccountTabs = [
  { value: "all", label: "All", count: "all" },
  { value: "unverified", label: "Unverified", count: "unverified" },
  { value: "suspended", label: "Suspended", count: "suspended" },
] as const;

// Contact in the list is masked by the server; the opened account shows the full details.
export interface CustomerAccountRow {
  id: string;
  name: string;
  email: string | null;
  mobile: string | null;
  verified: boolean;
  suspended: boolean;
  closed: boolean;
}

interface CustomerAccountCounts {
  all: number;
  unverified: number;
  suspended: number;
}

export interface CustomerAccountPage {
  actor: CurrentActor;
  items: CustomerAccountRow[];
  total: number;
  page: number;
  pageSize: number;
  counts: CustomerAccountCounts;
}

export interface CustomerAccountDetail {
  actor: CurrentActor;
  id: string;
  name: string;
  username: string | null;
  email: string | null;
  mobile: string | null;
  address: string | null;
  nric: string | null;
  verified: boolean;
  suspended: boolean;
  closed: boolean;
}

export interface CustomerAccountFilters {
  view: string;
  search: string;
  name: string;
  email: string;
  mobile: string;
  sort: string;
  direction: "asc" | "desc";
  pageSize: number;
}

const sortKeys = ["name", "email", "mobile", "status"];

// The state a customer is shown in; a closed account outranks a suspension, which outranks a missing verification.
export function customerStatus(row: Pick<CustomerAccountRow, "verified" | "suspended" | "closed">) {
  if (row.closed) {
    return "Closed";
  }
  if (row.suspended) {
    return "Suspended";
  }
  return row.verified ? "Active" : "Unverified";
}

// A closed customer has no recorded contact; the server sends null for each removed field.
const isOptionalText = (value: unknown) => value === null || typeof value === "string";

function parseRow(value: unknown): CustomerAccountRow | null {
  return isRecord(value) &&
    isUuid(value.id) &&
    typeof value.name === "string" &&
    isOptionalText(value.email) &&
    isOptionalText(value.mobile) &&
    typeof value.verified === "boolean" &&
    typeof value.suspended === "boolean" &&
    typeof value.closed === "boolean"
    ? (value as unknown as CustomerAccountRow)
    : null;
}

export function parseCustomerAccountPage(value: unknown): CustomerAccountPage | null {
  const actor = isRecord(value) ? parseActor(value.actor) : null;
  const page = parsePage(value, parseRow);
  const counts = isRecord(value) ? value.counts : null;
  if (
    !actor ||
    !page ||
    !isRecord(counts) ||
    !isNonNegativeInteger(counts.all) ||
    !isNonNegativeInteger(counts.unverified) ||
    !isNonNegativeInteger(counts.suspended) ||
    new Set(page.items.map((row) => row.id)).size !== page.items.length
  ) {
    return null;
  }
  return {
    actor,
    items: page.items,
    total: page.total,
    page: page.page,
    pageSize: page.pageSize,
    counts: counts as unknown as CustomerAccountCounts,
  };
}

export function parseCustomerAccountDetail(value: unknown): CustomerAccountDetail | null {
  const actor = isRecord(value) ? parseActor(value.actor) : null;
  return actor &&
    isRecord(value) &&
    isUuid(value.id) &&
    typeof value.name === "string" &&
    isOptionalText(value.username) &&
    isOptionalText(value.email) &&
    isOptionalText(value.mobile) &&
    isOptionalText(value.address) &&
    isOptionalText(value.nric) &&
    typeof value.verified === "boolean" &&
    typeof value.suspended === "boolean" &&
    typeof value.closed === "boolean"
    ? ({ ...value, actor } as unknown as CustomerAccountDetail)
    : null;
}

const cleanText = (value: string) => value.replace(/[\x00-\x1f\x7f-\x9f]/g, "").slice(0, 160);

export function customerAccountParams(parameters: SearchParams) {
  const text = (key: string) => (typeof parameters[key] === "string" ? parameters[key] : "");
  const page = Number(text("page") || 1);
  const pageSize = Number(text("pageSize"));
  return {
    filters: {
      view: customerAccountTabs.some((tab) => tab.value === text("view")) ? text("view") : "all",
      search: cleanText(text("search")),
      name: cleanText(text("name")),
      email: cleanText(text("email")),
      mobile: cleanText(text("mobile")),
      sort: sortKeys.includes(text("sort")) ? text("sort") : "",
      direction: text("direction") === "desc" ? ("desc" as const) : ("asc" as const),
      pageSize: isPageSize(pageSize) ? pageSize : DEFAULT_PAGE_SIZE,
    } satisfies CustomerAccountFilters,
    page: Number.isSafeInteger(page) && page > 0 ? page : 1,
  };
}

function filterEntries(filters: CustomerAccountFilters) {
  return Object.entries(filters)
    .filter(
      ([key, value]) =>
        key !== "pageSize" &&
        value !== "" &&
        !(key === "view" && value === "all") &&
        // Ascending is the default order, so only a descending direction is carried.
        !(key === "direction" && (value === "asc" || !filters.sort)),
    )
    .map(([key, value]): [string, string] => [key, String(value)]);
}

export function customerAccountQuery(filters: CustomerAccountFilters, page = 1) {
  const query = paginationQuery(page, filters.pageSize);
  for (const [key, value] of filterEntries(filters)) {
    query.set(key, value);
  }
  return query.toString();
}

// The browser address omits every default: the first page, the standard page size and the All view.
export function customerAccountUrlQuery(filters: CustomerAccountFilters, page: number) {
  const query = new URLSearchParams(filterEntries(filters));
  if (page !== 1) {
    query.set("page", String(page));
  }
  if (filters.pageSize !== DEFAULT_PAGE_SIZE) {
    query.set("pageSize", String(filters.pageSize));
  }
  return query;
}
