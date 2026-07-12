import { parseActor, type CurrentActor } from "@/lib/auth/actor";
import { isNonNegativeInteger, isRecord, isUuid } from "@/lib/auth/contracts";
import { parsePage } from "@/lib/api";
import { DEFAULT_PAGE_SIZE, isPageSize, paginationQuery } from "@/lib/pagination";
import type { SearchParams } from "@/lib/query";

export const customerAccountTabs = [
  { value: "All", label: "All", count: "all" },
  { value: "Unverified", label: "Unverified", count: "unverified" },
  { value: "Suspended", label: "Suspended", count: "suspended" },
] as const;

// Contact in the list is masked by the server; the opened account shows the full details.
export interface CustomerAccountRow {
  id: string;
  name: string;
  email: string;
  mobile: string;
  verified: boolean;
  suspended: boolean;
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
  username: string;
  email: string;
  mobile: string;
  address: string;
  nric: string;
  verified: boolean;
  suspended: boolean;
}

export interface CustomerAccountFilters {
  tab: string;
  search: string;
  name: string;
  email: string;
  mobile: string;
  pageSize: number;
}

// The state a customer is shown in; a suspension outranks a missing verification.
export function customerStatus(row: Pick<CustomerAccountRow, "verified" | "suspended">) {
  if (row.suspended) {
    return "Suspended";
  }
  return row.verified ? "Active" : "Unverified";
}

function parseRow(value: unknown): CustomerAccountRow | null {
  return isRecord(value) &&
    isUuid(value.id) &&
    typeof value.name === "string" &&
    typeof value.email === "string" &&
    typeof value.mobile === "string" &&
    typeof value.verified === "boolean" &&
    typeof value.suspended === "boolean"
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
    typeof value.username === "string" &&
    typeof value.email === "string" &&
    typeof value.mobile === "string" &&
    typeof value.address === "string" &&
    typeof value.nric === "string" &&
    typeof value.verified === "boolean" &&
    typeof value.suspended === "boolean"
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
      tab: customerAccountTabs.some((tab) => tab.value === text("tab")) ? text("tab") : "All",
      search: cleanText(text("search")),
      name: cleanText(text("name")),
      email: cleanText(text("email")),
      mobile: cleanText(text("mobile")),
      pageSize: isPageSize(pageSize) ? pageSize : DEFAULT_PAGE_SIZE,
    } satisfies CustomerAccountFilters,
    page: Number.isSafeInteger(page) && page > 0 ? page : 1,
  };
}

export function customerAccountQuery(filters: CustomerAccountFilters, page = 1) {
  const query = paginationQuery(page, filters.pageSize);
  for (const [key, value] of Object.entries(filters)) {
    if (key !== "pageSize" && value !== "" && !(key === "tab" && value === "All")) {
      query.set(key, String(value));
    }
  }
  return query.toString();
}

export const customerAccountsUrl = (filters: CustomerAccountFilters, page: number) =>
  `/dashboard/accounts?view=customers&${customerAccountQuery(filters, page)}`;
