import type { CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";
import { isUuid } from "@/lib/auth/contracts";
import { DEFAULT_PAGE_SIZE, isPageSize, paginationQuery } from "@/lib/pagination";
import { parseListingRecord } from "./listing-history-selection";

export const vehicleStatuses = ["PendingInspection", "Rejected", "Available"] as const;
export const listingSections = ["listing", "card", "publication", "history"] as const;
export type ListingSection = (typeof listingSections)[number];
export const listingSorts = [
  "registration",
  "vehicle",
  "branch",
  "inspection",
  "price",
  "publication",
] as const;

export interface ListingItem {
  vehicleId: string;
  branchId: string;
  branchName: string;
  registrationPlate: string;
  make: string;
  model: string;
  trim: string;
  manufactureYear: number;
  mileageKilometres: number;
  vehicleStatus: (typeof vehicleStatuses)[number];
  revisionId: string | null;
  version: number;
  vehiclePrice: number | null;
  publicationId: string | null;
  publishedRevisionId: string | null;
  isWithdrawn: boolean;
  isPublic: boolean;
}

export interface ListingFilters {
  search: string;
  branchId: string;
  pageSize: number;
  sort: string;
  direction: "asc" | "desc";
}

export function hasListingAccess(actor: CurrentActor) {
  return actor.role === "BranchManager" || actor.role === "GeneralManager";
}

export function listingParams(params: SearchParams) {
  const size = Number(params.pageSize);
  const page = Number(params.page);
  const filters: ListingFilters = {
    search: typeof params.search === "string" ? params.search.trim() : "",
    branchId: isUuid(params.branchId) ? params.branchId : "",
    pageSize: isPageSize(size) ? size : DEFAULT_PAGE_SIZE,
    sort: listingSorts.find((value) => value === params.sort) ?? "",
    direction: params.direction === "desc" ? "desc" : "asc",
  };
  return {
    filters,
    page: Number.isSafeInteger(page) && page > 0 ? page : 1,
    listingId: isUuid(params.listing) ? params.listing : "",
    section: listingSections.find((section) => section === params.section) ?? "listing",
    record: parseListingRecord(params),
  };
}

export function listingQuery(filters: ListingFilters, page = 1) {
  const query = paginationQuery(page, filters.pageSize);
  if (filters.sort) {
    query.set("sort", filters.sort);
    query.set("direction", filters.direction);
  }
  if (filters.search) {
    query.set("search", filters.search);
  }
  if (filters.branchId) {
    query.set("branchId", filters.branchId);
  }
  return query.toString();
}

export function listingPublication(item: ListingItem) {
  if (item.isPublic) {
    return item.revisionId === item.publishedRevisionId ? "Published" : "Published · draft changes";
  }
  if (item.isWithdrawn) {
    return "Withdrawn";
  }
  if (item.publicationId) {
    return "Approved · unavailable";
  }
  return item.revisionId ? "Draft" : "Not prepared";
}
