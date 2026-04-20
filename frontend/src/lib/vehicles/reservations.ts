import type { CurrentActor } from "@/lib/auth/actor";
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
] as const;

export const reservationStatuses = {
  Active: "Active hold",
  CustomerCancelled: "Customer cancelled",
  StaffCancelled: "Staff cancelled",
  Expired: "Expired",
  Withdrawn: "Listing withdrawn",
  Reinspection: "Reinspection",
} as const;

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
}

export interface ReservationFilters {
  branchId: string;
  isActive: boolean;
  pageSize: number;
  sort: string;
  direction: "asc" | "desc";
  search?: string;
  registration?: string;
  vehicle?: string;
  buyer?: string;
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

export function reservationViewFilters(
  filters: ReservationFilters,
  isActive: boolean,
): ReservationFilters {
  if (filters.isActive === isActive) {
    return filters;
  }
  let sort = filters.sort;
  if (sort === "expires" || sort === "released") {
    sort = isActive ? "expires" : "released";
  }
  return {
    ...filters,
    isActive,
    sort,
    status: "",
    expiresFrom: "",
    expiresTo: "",
    releasedFrom: "",
    releasedTo: "",
  };
}
