import type { CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";
import { isUuid } from "@/lib/auth/contracts";
import { DEFAULT_PAGE_SIZE, isPageSize, paginationQuery } from "@/lib/pagination";
import { parseListingRecord } from "./listing-history-selection";

export const vehicleStatuses = ["PendingInspection", "Rejected", "Available"] as const;
export const listingInspectionOptions = [
  { value: "", label: "All" },
  { value: "PendingInspection", label: "Not cleared" },
  { value: "Rejected", label: "Rejected" },
  { value: "Available", label: "Cleared" },
];
export const listingPublicationOptions = [
  { value: "", label: "All listings" },
  { value: "unprepared", label: "Not prepared" },
  { value: "draft", label: "Draft" },
  { value: "published", label: "Published" },
  { value: "changes", label: "Published with saved changes" },
  { value: "unavailable", label: "Hidden" },
  { value: "withdrawn", label: "Withdrawn" },
];
export const listingSections = ["listing", "card", "publication", "history"] as const;
export type ListingSection = (typeof listingSections)[number];
export const listingSorts = [
  "registration",
  "vehicle",
  "branch",
  "inspection",
  "price",
  "publication",
  "publishedPrice",
  "activity",
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
  approvedVehiclePrice: number | null;
  savedAt: string | null;
  approvedAt: string | null;
  withdrawnAt: string | null;
  isReserved: boolean;
  hasCurrentPublicationInspection: boolean;
}

export interface ListingFilters {
  registration?: string;
  vehicle?: string;
  search: string;
  branchId: string;
  inspection?: string;
  publication?: string;
  minimumPrice?: string;
  maximumPrice?: string;
  minimumPublishedPrice?: string;
  maximumPublishedPrice?: string;
  from?: string;
  to?: string;
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
    registration: typeof params.registration === "string" ? params.registration : "",
    vehicle: typeof params.vehicle === "string" ? params.vehicle : "",
    branchId: isUuid(params.branchId) ? params.branchId : "",
    inspection:
      listingInspectionOptions.find((option) => option.value === params.inspection)?.value ?? "",
    publication:
      listingPublicationOptions.find((option) => option.value === params.publication)?.value ?? "",
    minimumPrice: typeof params.minimumPrice === "string" ? params.minimumPrice : "",
    maximumPrice: typeof params.maximumPrice === "string" ? params.maximumPrice : "",
    minimumPublishedPrice:
      typeof params.minimumPublishedPrice === "string" ? params.minimumPublishedPrice : "",
    maximumPublishedPrice:
      typeof params.maximumPublishedPrice === "string" ? params.maximumPublishedPrice : "",
    from: typeof params.from === "string" ? params.from : "",
    to: typeof params.to === "string" ? params.to : "",
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
  for (const key of [
    "minimumPrice",
    "maximumPrice",
    "minimumPublishedPrice",
    "maximumPublishedPrice",
    "from",
    "to",
    "registration",
    "vehicle",
  ] as const) {
    if (filters[key]) {
      query.set(key, filters[key]);
    }
  }
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
  if (filters.inspection) {
    query.set("inspection", filters.inspection);
  }
  if (filters.publication) {
    query.set("publication", filters.publication);
  }
  return query.toString();
}

export function listingPublicationState(item: ListingItem) {
  if (item.isPublic) {
    return { label: "Published", tone: "success" } as const;
  }
  if (item.isWithdrawn) {
    return { label: "Withdrawn", tone: "neutral" } as const;
  }
  if (item.publicationId) {
    return { label: "Hidden", tone: "warning" } as const;
  }
  return { label: item.revisionId ? "Draft" : "Not prepared", tone: "neutral" } as const;
}

export function listingPublication(item: ListingItem) {
  const state = listingPublicationState(item);
  if (item.isPublic && item.revisionId !== item.publishedRevisionId) {
    return `${state.label} · saved changes not live`;
  }
  return state.label;
}
