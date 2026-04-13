import { parsePage } from "@/lib/api";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { parseRevision, type ListingRevision, type ListingWorkspace } from "./listing-workspace";
import { listingHistoryKinds, type ListingRecordSelection } from "./listing-history-selection";
import {
  parseListingPublicationReceipt,
  type ListingPublicationReceipt,
} from "./listing-publication";
import {
  parseListingWithdrawalReceipt,
  type ListingWithdrawalReceipt,
} from "./listing-withdrawal";

export interface ListingHistoryEntry {
  id: string;
  kind: (typeof listingHistoryKinds)[number];
  revisionId: string;
  version: number;
  actorId: string;
  actorIdentity: string;
  recordedAt: string;
  reason: string | null;
}

export interface ListingHistoryPage {
  vehicleId: string;
  branchId: string;
  items: ListingHistoryEntry[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ListingRevisionRead {
  vehicleId: string;
  branchId: string;
  revision: ListingRevision;
  signedReport: ListingWorkspace["currentSignedReport"];
  publication: ListingPublicationReceipt | null;
  withdrawal: ListingWithdrawalReceipt | null;
}

function parseEntry(value: unknown): ListingHistoryEntry | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !isUuid(value.revisionId) ||
    !isUuid(value.actorId) ||
    !Number.isSafeInteger(value.version) ||
    Number(value.version) < 1 ||
    typeof value.actorIdentity !== "string" ||
    !value.actorIdentity.trim() ||
    typeof value.recordedAt !== "string" ||
    !/(?:Z|\+00:00)$/.test(value.recordedAt) ||
    !Number.isFinite(Date.parse(value.recordedAt))
  ) {
    return null;
  }
  const kind = listingHistoryKinds.find((name) => name === value.kind);
  if (
    !kind ||
    (kind === "DraftSaved" && value.id !== value.revisionId) ||
    (kind === "ListingWithdrawn" &&
      (typeof value.reason !== "string" || !value.reason.trim() || value.reason.length > 1000)) ||
    (kind !== "ListingWithdrawn" && value.reason !== null)
  ) {
    return null;
  }
  return {
    id: value.id,
    kind,
    revisionId: value.revisionId,
    version: Number(value.version),
    actorId: value.actorId,
    actorIdentity: value.actorIdentity,
    recordedAt: value.recordedAt,
    reason: typeof value.reason === "string" ? value.reason : null,
  };
}

export function parseListingHistory(value: unknown): ListingHistoryPage | null {
  if (!isRecord(value) || !isUuid(value.vehicleId) || !isUuid(value.branchId)) {
    return null;
  }
  const page = parsePage({ ...value, total: value.totalCount }, parseEntry);
  if (
    !page ||
    new Set(page.items.map((item) => `${item.kind}:${item.id}`)).size !== page.items.length
  ) {
    return null;
  }
  return {
    vehicleId: value.vehicleId,
    branchId: value.branchId,
    items: page.items,
    totalCount: page.total,
    page: page.page,
    pageSize: page.pageSize,
  };
}

export function parseListingRevisionRead(value: unknown): ListingRevisionRead | null {
  if (!isRecord(value) || !isUuid(value.vehicleId) || !isUuid(value.branchId)) {
    return null;
  }
  const revision = parseRevision(value.revision);
  if (!revision || revision.vehicleId !== value.vehicleId || revision.branchId !== value.branchId) {
    return null;
  }
  let signedReport: ListingRevisionRead["signedReport"] = null;
  if (value.signedReport !== null) {
    const report = value.signedReport;
    if (
      !isRecord(report) ||
      !isUuid(report.inspectionId) ||
      !Number.isSafeInteger(report.revision) ||
      Number(report.revision) < 1
    ) {
      return null;
    }
    signedReport = { inspectionId: report.inspectionId, revision: Number(report.revision) };
  }
  const publication =
    value.publication === null ? null : parseListingPublicationReceipt(value.publication);
  const withdrawal =
    value.withdrawal === null ? null : parseListingWithdrawalReceipt(value.withdrawal);
  if (
    (value.publication !== null && !publication) ||
    (value.withdrawal !== null && !withdrawal) ||
    (publication &&
      (publication.vehicleId !== value.vehicleId ||
        publication.branchId !== value.branchId ||
        publication.revisionId !== revision.id)) ||
    (withdrawal &&
      (!publication ||
        withdrawal.publicationId !== publication.id ||
        withdrawal.vehicleId !== value.vehicleId ||
        withdrawal.branchId !== value.branchId)) ||
    Boolean(publication) !== Boolean(signedReport)
  ) {
    return null;
  }
  return {
    vehicleId: value.vehicleId,
    branchId: value.branchId,
    revision,
    signedReport,
    publication,
    withdrawal,
  };
}

export function matchesListingRecord(value: ListingRevisionRead, record: ListingRecordSelection) {
  if (value.revision.id !== record.revisionId) {
    return false;
  }
  if (record.kind === "DraftSaved") {
    return value.revision.id === record.eventId;
  }
  if (record.kind === "PublicationApproved") {
    return value.publication?.id === record.eventId;
  }
  return value.withdrawal?.id === record.eventId;
}

export function appendListingHistory(previous: ListingHistoryPage, next: ListingHistoryPage) {
  const items = new Map(
    [...previous.items, ...next.items].map((item) => [`${item.kind}:${item.id}`, item]),
  );
  return { ...next, items: [...items.values()] };
}
