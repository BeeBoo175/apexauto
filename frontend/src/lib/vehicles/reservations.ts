import type { CurrentActor } from "@/lib/auth/actor";
import type { CountedPage } from "@/lib/api";
import { isUuid } from "@/lib/auth/contracts";
import { DEFAULT_PAGE_SIZE, isPageSize, paginationQuery } from "@/lib/pagination";
import type { SearchParams } from "@/lib/query";

export const reservationSorts = [
  "reference",
  "registration",
  "released",
  "vehicle",
  "buyer",
  "branch",
  "created",
  "expires",
  "total",
  "status",
  "agent",
  "claimed",
] as const;

export const reservationStatuses = {
  Active: "Active hold",
  CustomerCancelled: "Customer cancelled",
  StaffCancelled: "Staff cancelled",
  Expired: "Expired",
  Withdrawn: "Listing withdrawn",
  Reinspection: "Reinspection",
} as const;

// The agent who took the hold and how much of its time stood still when they did.
export interface ReservationClaim {
  agent: string;
  claimedAtUtc: string;
  frozenRemainingSeconds: number;
}

export interface ReservationItem {
  id: string;
  vehicleId: string;
  branchId: string;
  branchName: string;
  vehicle: string;
  registrationPlate: string;
  make: string;
  model: string;
  trim: string;
  manufactureYear: number;
  releasedAt: string | null;
  buyer: string;
  createdAt: string;
  expiresAt: string;
  total: number;
  status: keyof typeof reservationStatuses;
  claim: ReservationClaim | null;
}

export const reservationCountKeys = ["active", "history", "open", "handled"] as const;
export type ReservationPage = CountedPage<ReservationItem, (typeof reservationCountKeys)[number]>;

export interface ReservationFilters {
  branchId: string;
  isActive: boolean;
  claimed: boolean;
  pageSize: number;
  sort: string;
  direction: "asc" | "desc";
  search?: string;
  registration?: string;
  vehicle?: string;
  buyer?: string;
  agent?: string;
  status?: string;
  minimumTotal?: string;
  maximumTotal?: string;
  createdFrom?: string;
  createdTo?: string;
  expiresFrom?: string;
  expiresTo?: string;
  releasedFrom?: string;
  releasedTo?: string;
}

export const reservationFilterDefaults = {
  search: "",
  registration: "",
  vehicle: "",
  buyer: "",
  agent: "",
  status: "",
  minimumTotal: "",
  maximumTotal: "",
  createdFrom: "",
  createdTo: "",
  expiresFrom: "",
  expiresTo: "",
  releasedFrom: "",
  releasedTo: "",
};

export const reservationFilterKeys = Object.keys(
  reservationFilterDefaults,
) as (keyof typeof reservationFilterDefaults)[];

export function hasReservationAccess(actor: CurrentActor) {
  return actor.role === "BranchManager" || actor.role === "GeneralManager";
}

export function reservationParams(params: SearchParams) {
  const page = Number(params.page);
  const size = Number(params.pageSize);
  const filters: ReservationFilters = {
    ...reservationFilterDefaults,
    branchId: isUuid(params.branchId) ? params.branchId : "",
    isActive: params.isActive !== "false",
    claimed: params.isActive !== "false" && params.claimed === "true",
    pageSize: isPageSize(size) ? size : DEFAULT_PAGE_SIZE,
    sort: reservationSorts.find((sort) => sort === params.sort) ?? "",
    direction: params.direction === "desc" ? "desc" : "asc",
  };
  for (const key of reservationFilterKeys) {
    const value = params[key];
    if (typeof value === "string") {
      filters[key] = value;
    }
  }
  return { filters, page: Number.isSafeInteger(page) && page > 0 ? page : 1 };
}

export function reservationQuery(filters: ReservationFilters, page = 1) {
  const query = paginationQuery(page, filters.pageSize);
  query.set("isActive", String(filters.isActive));
  if (filters.isActive) {
    query.set("claimed", String(filters.claimed));
  }
  if (filters.branchId) {
    query.set("branchId", filters.branchId);
  }
  if (filters.sort) {
    query.set("sort", filters.sort);
    query.set("direction", filters.direction);
  }
  for (const key of reservationFilterKeys) {
    const value = filters[key]?.trim();
    if (value) {
      query.set(key, value);
    }
  }
  return query.toString();
}

export type ReservationView = "open" | "handled" | "history";

export function reservationView(filters: ReservationFilters): ReservationView {
  if (!filters.isActive) {
    return "history";
  }
  return filters.claimed ? "handled" : "open";
}

// Rows belong to the view that asked for them, so a mismatched response is never shown as that view.
export function reservationRowsFit(filters: ReservationFilters, items: ReservationItem[]) {
  const view = reservationView(filters);
  return items.every((row) => {
    if (view === "history") {
      return row.status !== "Active";
    }
    return row.status === "Active" && (row.claim !== null) === (view === "handled");
  });
}

export function reservationViewFilters(
  filters: ReservationFilters,
  view: ReservationView,
): ReservationFilters {
  if (reservationView(filters) === view) {
    return filters;
  }
  let sort = filters.sort;
  if (sort === "expires" || sort === "released") {
    sort = { open: "expires", handled: "", history: "released" }[view];
  } else if ((sort === "agent" || sort === "claimed") && view !== "handled") {
    sort = "";
  }
  return {
    ...filters,
    isActive: view !== "history",
    claimed: view === "handled",
    sort,
    agent: "",
    status: "",
    expiresFrom: "",
    expiresTo: "",
    releasedFrom: "",
    releasedTo: "",
  };
}
