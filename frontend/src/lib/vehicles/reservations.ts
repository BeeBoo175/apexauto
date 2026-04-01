import type { CurrentActor } from "@/lib/auth/actor";
import { isUuid } from "@/lib/auth/contracts";
import { DEFAULT_PAGE_SIZE, isPageSize, paginationQuery } from "@/lib/pagination";
import type { SearchParams } from "@/lib/query";

export const reservationSorts = [
  "reference",
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
}

export function hasReservationAccess(actor: CurrentActor) {
  return actor.role === "BranchManager" || actor.role === "GeneralManager";
}

export function reservationParams(params: SearchParams) {
  const page = Number(params.page);
  const size = Number(params.pageSize);
  const filters: ReservationFilters = {
    branchId: isUuid(params.branchId) ? params.branchId : "",
    isActive: params.isActive !== "false",
    pageSize: isPageSize(size) ? size : DEFAULT_PAGE_SIZE,
    sort: reservationSorts.find((sort) => sort === params.sort) ?? "",
    direction: params.direction === "desc" ? "desc" : "asc",
  };
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
  return query.toString();
}
