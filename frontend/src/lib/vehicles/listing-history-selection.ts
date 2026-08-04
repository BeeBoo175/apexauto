import { isUuid } from "@/lib/auth/contracts";
import type { SearchParams } from "@/lib/query";

export const listingHistoryKinds = [
  "DraftSaved",
  "PublicationApproved",
  "ListingWithdrawn",
] as const;

export interface ListingRecordSelection {
  kind: (typeof listingHistoryKinds)[number];
  revisionId: string;
  eventId: string;
}

export function parseListingRecord(params: SearchParams): ListingRecordSelection | null {
  const kind = listingHistoryKinds.find((value) => value === params.eventKind);
  if (!kind || !isUuid(params.revision) || !isUuid(params.event)) {
    return null;
  }
  return { kind, revisionId: params.revision, eventId: params.event };
}

export function listingRecordSection(record: ListingRecordSelection) {
  return record.kind === "DraftSaved" ? "listing" : "publication";
}
