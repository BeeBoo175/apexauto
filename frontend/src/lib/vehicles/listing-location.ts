import type { SearchParams } from "@/lib/query";
import { isUuid } from "@/lib/auth/contracts";
import { DEFAULT_PAGE_SIZE } from "@/lib/pagination";
import { staffRecordLocation } from "@/lib/staff/record-location";
import {
  listingParams,
  listingQueryToken,
  listingSections,
  type ListingFilters,
  type ListingSection,
} from "./listings";
import {
  listingHistoryKinds,
  listingRecordSection,
  type ListingRecordSelection,
} from "./listing-history-selection";

export const listingWorkspace = "/dashboard/management";

export type ListingSelectionPath = {
  id: string;
  section: ListingSection;
  record: ListingRecordSelection | null;
};

export type ListingState = ListingSelectionPath & {
  filters: ListingFilters;
  page: number;
};

// Shapes: base, /<id> (Listing), /<id>/<card|publication|history> and a retained history record,
// /<id>/<listing|publication>/revisions/<revisionId>/<eventKind>/<eventId>, whose section follows its kind.
export function listingSelectionPath(pathname: string): ListingSelectionPath | null {
  const record = staffRecordLocation(pathname, listingWorkspace);
  if (!record) {
    return null;
  }
  const { id, section, trail } = record;
  if (section === "" && trail.length === 0) {
    return { id, section: "listing", record: null };
  }
  const named = listingSections.find((value) => value === section);
  if (!named) {
    return null;
  }
  if (trail.length === 0) {
    return named === "listing" ? null : { id, section: named, record: null };
  }
  const [keyword, revisionId, kindToken, eventId, ...extra] = trail;
  const kind = listingHistoryKinds.find((value) => listingQueryToken(value) === kindToken);
  if (
    keyword !== "revisions" ||
    !kind ||
    !isUuid(revisionId) ||
    !isUuid(eventId) ||
    extra.length > 0
  ) {
    return null;
  }
  const retained = { kind, revisionId, eventId };
  return listingRecordSection(retained) === named ? { id, section: named, record: retained } : null;
}

export function listingSelectionRoute(id: string, section: string[] = []) {
  const path = listingSelectionPath(`${listingWorkspace}/${[id, ...section].join("/")}`);
  return path && path.id ? path : null;
}

export function listingSelectionHref({ id, section, record }: ListingSelectionPath) {
  if (!id) {
    return listingWorkspace;
  }
  if (record) {
    const kind = listingQueryToken(record.kind);
    return `${listingWorkspace}/${id}/${section}/revisions/${record.revisionId}/${kind}/${record.eventId}`;
  }
  return section === "listing"
    ? `${listingWorkspace}/${id}`
    : `${listingWorkspace}/${id}/${section}`;
}

const textKeys = [
  "search",
  "branchId",
  "minimumPrice",
  "maximumPrice",
  "minimumPublishedPrice",
  "maximumPublishedPrice",
  "from",
  "to",
  "registration",
  "vehicle",
] as const;

export function listingUrlQuery(filters: ListingFilters, page: number) {
  const query = new URLSearchParams();
  if (page !== 1) {
    query.set("page", String(page));
  }
  if (filters.pageSize !== DEFAULT_PAGE_SIZE) {
    query.set("pageSize", String(filters.pageSize));
  }
  if (filters.sort) {
    query.set("sort", filters.sort);
    query.set("direction", filters.direction);
  }
  for (const key of textKeys) {
    const value = (filters[key] ?? "").trim();
    if (value) {
      query.set(key, value);
    }
  }
  if (filters.inspection) {
    query.set("inspection", listingQueryToken(filters.inspection));
  }
  if (filters.publication) {
    query.set("view", filters.publication);
  }
  return query;
}

export function listingHref(state: ListingState) {
  const query = listingUrlQuery(state.filters, state.page);
  const path = listingSelectionHref(state);
  return query.size ? `${path}?${query}` : path;
}

export function listingLocation(pathname: string, search: string) {
  const selection = listingSelectionPath(pathname);
  if (!selection) {
    return null;
  }
  const { filters, page } = listingParams(Object.fromEntries(new URLSearchParams(search)));
  const state: ListingState = { ...selection, filters, page };
  return { ...state, href: listingHref(state) };
}

// The server page entry merges the path selection into the entry parameters.
export function listingEntryParameters(query: SearchParams, selection: ListingSelectionPath) {
  const rest = { ...query };
  for (const key of ["listing", "section", "revision", "event", "eventKind"]) {
    delete rest[key];
  }
  if (!selection.id) {
    return rest;
  }
  return {
    ...rest,
    listing: selection.id,
    section: selection.section,
    ...(selection.record
      ? {
          revision: selection.record.revisionId,
          event: selection.record.eventId,
          eventKind: selection.record.kind,
        }
      : {}),
  };
}
