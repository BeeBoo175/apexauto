import { parseActor, type CurrentActor } from "@/lib/auth/actor";
import { isRecord, isUtcInstant, isUuid } from "@/lib/auth/contracts";
import { isNonNegativeInteger } from "@/lib/auth/contracts";
import { parsePage } from "@/lib/api";
import { DEFAULT_PAGE_SIZE, isPageSize, paginationQuery } from "@/lib/pagination";
import type { SearchParams } from "@/lib/query";

export const staffAccountTabs = [
  { value: "all", label: "All", count: "all" },
  { value: "active", label: "Active", count: "active" },
  { value: "invited", label: "Invited", count: "invited" },
  { value: "suspended", label: "Suspended", count: "suspended" },
  { value: "deactivated", label: "Deactivated", count: "deactivated" },
] as const;

export const staffAccountStatusLabels: Record<string, string> = {
  Active: "Active",
  Deactivated: "Deactivated",
  Invited: "Invited",
  Suspended: "Suspended",
  Unverified: "Email not verified",
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

// The address and API name a role in camelCase; the role itself keeps its DTO casing.
export const staffRoleToken = (role: string) => `${role.charAt(0).toLowerCase()}${role.slice(1)}`;

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

interface StaffAccountCounts {
  all: number;
  active: number;
  invited: number;
  suspended: number;
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
  view: string;
  search: string;
  role: string;
  branchId: string;
  name: string;
  email: string;
  mobile: string;
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
    !isNonNegativeInteger(value.suspended) ||
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
      view: staffAccountTabs.some((tab) => tab.value === text("view")) ? text("view") : "all",
      search: cleanText(text("search")),
      role: staffAccountRoles.find((role) => staffRoleToken(role) === text("role")) ?? "",
      branchId: isUuid(text("branchId")) ? text("branchId") : "",
      name: cleanText(text("name")),
      email: cleanText(text("email")),
      mobile: cleanText(text("mobile")),
      pageSize: isPageSize(pageSize) ? pageSize : DEFAULT_PAGE_SIZE,
    } satisfies StaffAccountFilters,
    page: Number.isSafeInteger(page) && page > 0 ? page : 1,
  };
}

function filterEntries(filters: StaffAccountFilters) {
  const entries: [string, string][] = [];
  for (const [key, value] of Object.entries(filters)) {
    if (key === "pageSize" || value === "" || (key === "view" && value === "all")) {
      continue;
    }
    entries.push([key, key === "role" ? staffRoleToken(String(value)) : String(value)]);
  }
  return entries;
}

export function staffAccountQuery(filters: StaffAccountFilters, page = 1) {
  const query = paginationQuery(page, filters.pageSize);
  for (const [key, value] of filterEntries(filters)) {
    query.set(key, value);
  }
  return query.toString();
}

// The browser address omits every default: the first page, the standard page size and the All view.
export function staffAccountUrlQuery(filters: StaffAccountFilters, page: number) {
  const query = new URLSearchParams(filterEntries(filters));
  if (page !== 1) {
    query.set("page", String(page));
  }
  if (filters.pageSize !== DEFAULT_PAGE_SIZE) {
    query.set("pageSize", String(filters.pageSize));
  }
  return query;
}
