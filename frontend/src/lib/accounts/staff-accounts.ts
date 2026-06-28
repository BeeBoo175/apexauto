import { parseActor, type CurrentActor } from "@/lib/auth/actor";
import { isRecord, isUtcInstant, isUuid } from "@/lib/auth/contracts";
import { isNonNegativeInteger } from "@/lib/auth/contracts";
import { parsePage } from "@/lib/api";
import { DEFAULT_PAGE_SIZE, isPageSize, paginationQuery } from "@/lib/pagination";
import type { SearchParams } from "@/lib/query";

export const staffAccountTabs = [
  { value: "All", label: "All", count: "all" },
  { value: "Active", label: "Active", count: "active" },
  { value: "Invited", label: "Invited", count: "invited" },
  { value: "Deactivated", label: "Deactivated", count: "deactivated" },
] as const;

export const staffAccountStatusLabels: Record<string, string> = {
  Active: "Active",
  Deactivated: "Deactivated",
  Invited: "Invited",
  InvitationExpired: "Invitation expired",
};

// Staff roles an Administrator can filter by; customers have their own view.
export const staffAccountRoles = [
  "SalesAgent",
  "WorkshopTechnician",
  "BranchManager",
  "GeneralManager",
  "Administrator",
] as const;

export const hasAccountAdministration = (actor: Pick<CurrentActor, "role">) =>
  actor.role === "Administrator";

// Id is the account for an Active or Deactivated row and the invitation for an Invited one.
export interface StaffAccountRow {
  id: string;
  name: string;
  email: string;
  mobile: string;
  role: string;
  branchId: string | null;
  branchName: string | null;
  status: string;
  pictureId: string | null;
  invitationExpiresAt: string | null;
}

export interface StaffAccountCounts {
  all: number;
  active: number;
  invited: number;
  deactivated: number;
}

export interface StaffAccountPage {
  actor: CurrentActor;
  items: StaffAccountRow[];
  total: number;
  page: number;
  pageSize: number;
  counts: StaffAccountCounts;
}

export interface StaffAccountFilters {
  tab: string;
  search: string;
  role: string;
  branchId: string;
  pageSize: number;
}

function parseRow(value: unknown): StaffAccountRow | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    typeof value.name !== "string" ||
    typeof value.email !== "string" ||
    typeof value.mobile !== "string" ||
    typeof value.role !== "string" ||
    !(value.branchId === null || isUuid(value.branchId)) ||
    !(value.branchName === null || typeof value.branchName === "string") ||
    typeof value.status !== "string" ||
    !Object.hasOwn(staffAccountStatusLabels, value.status) ||
    !(value.pictureId === null || isUuid(value.pictureId)) ||
    !(value.invitationExpiresAt === null || isUtcInstant(value.invitationExpiresAt))
  ) {
    return null;
  }
  return value as unknown as StaffAccountRow;
}

function parseCounts(value: unknown): StaffAccountCounts | null {
  if (
    !isRecord(value) ||
    !isNonNegativeInteger(value.all) ||
    !isNonNegativeInteger(value.active) ||
    !isNonNegativeInteger(value.invited) ||
    !isNonNegativeInteger(value.deactivated)
  ) {
    return null;
  }
  return value as unknown as StaffAccountCounts;
}

export function parseStaffAccountPage(value: unknown): StaffAccountPage | null {
  const actor = isRecord(value) ? parseActor(value.actor) : null;
  const page = parsePage(isRecord(value) ? { ...value, total: value.total } : value, parseRow);
  const counts = isRecord(value) ? parseCounts(value.counts) : null;
  if (
    !actor ||
    !page ||
    !counts ||
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
    counts,
  };
}

const cleanText = (value: string) => value.replace(/[\x00-\x1f\x7f-\x9f]/g, "").slice(0, 160);

export function staffAccountParams(parameters: SearchParams) {
  const text = (key: string) => (typeof parameters[key] === "string" ? parameters[key] : "");
  const page = Number(text("page") || 1);
  const pageSize = Number(text("pageSize"));
  return {
    filters: {
      tab: staffAccountTabs.some((tab) => tab.value === text("tab")) ? text("tab") : "All",
      search: cleanText(text("search")),
      role: staffAccountRoles.some((role) => role === text("role")) ? text("role") : "",
      branchId: isUuid(text("branchId")) ? text("branchId") : "",
      pageSize: isPageSize(pageSize) ? pageSize : DEFAULT_PAGE_SIZE,
    } satisfies StaffAccountFilters,
    page: Number.isSafeInteger(page) && page > 0 ? page : 1,
  };
}

export function staffAccountQuery(filters: StaffAccountFilters, page = 1) {
  const query = paginationQuery(page, filters.pageSize);
  for (const [key, value] of Object.entries(filters)) {
    if (key !== "pageSize" && value !== "" && !(key === "tab" && value === "All")) {
      query.set(key, String(value));
    }
  }
  return query.toString();
}

export const staffAccountsUrl = (filters: StaffAccountFilters, page: number) =>
  `/dashboard/accounts?${staffAccountQuery(filters, page)}`;
