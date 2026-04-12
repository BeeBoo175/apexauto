import { isRecord, isUuid } from "@/lib/auth/contracts";
import { isMyr } from "@/lib/currency-input";
import { fuels, transmissions } from "./contracts";
import { parseListing } from "./parse-listings";
import type { ListingItem } from "./listings";

export interface ListingRevision {
  id: string;
  vehicleId: string;
  branchId: string;
  version: number;
  vehiclePrice: number;
  roadTax: number;
  transmission: (typeof transmissions)[number];
  fuel: (typeof fuels)[number];
  description: string;
  recordedAt: string;
  photoIds: string[];
}

export interface ListingWorkspace {
  vehicle: ListingItem;
  draft: ListingRevision | null;
  approvedRevision: ListingRevision | null;
  publication: {
    id: string;
    vehicleId: string;
    branchId: string;
    revisionId: string;
    signedRevisionId: string;
    recordedAt: string;
  } | null;
  withdrawal: {
    publicationId: string;
    vehicleId: string;
    branchId: string;
    reason: string;
    recordedAt: string;
  } | null;
  currentSignedRevisionId: string | null;
  currentSignedReport: { inspectionId: string; revision: number } | null;
}

const date = (value: unknown): value is string =>
  typeof value === "string" && /(?:Z|\+00:00)$/.test(value) && Number.isFinite(Date.parse(value));

export function parseRevision(value: unknown): ListingRevision | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !isUuid(value.vehicleId) ||
    !isUuid(value.branchId) ||
    !Number.isSafeInteger(value.version) ||
    Number(value.version) < 1 ||
    !isMyr(value.vehiclePrice) ||
    !isMyr(value.roadTax) ||
    typeof value.description !== "string" ||
    !value.description.trim() ||
    value.description.length > 2000 ||
    !date(value.recordedAt) ||
    !Array.isArray(value.photoIds) ||
    !value.photoIds.every(isUuid) ||
    new Set(value.photoIds).size !== value.photoIds.length
  ) {
    return null;
  }
  const transmission = transmissions.find(
    (name, index) => name === value.transmission || index === value.transmission,
  );
  const fuel = fuels.find((name, index) => name === value.fuel || index === value.fuel);
  if (!transmission || !fuel) {
    return null;
  }
  return {
    id: value.id,
    vehicleId: value.vehicleId,
    branchId: value.branchId,
    version: Number(value.version),
    vehiclePrice: value.vehiclePrice,
    roadTax: value.roadTax,
    transmission,
    fuel,
    description: value.description,
    recordedAt: value.recordedAt,
    photoIds: value.photoIds,
  };
}

export function parseListingWorkspace(value: unknown): ListingWorkspace | null {
  if (!isRecord(value)) {
    return null;
  }
  const vehicle = parseListing(value.vehicle);
  const draft = value.draft === null ? null : parseRevision(value.draft);
  const approvedRevision =
    value.approvedRevision === null ? null : parseRevision(value.approvedRevision);
  if (
    !vehicle ||
    (value.draft !== null && !draft) ||
    (value.approvedRevision !== null && !approvedRevision)
  ) {
    return null;
  }
  for (const revision of [draft, approvedRevision]) {
    if (
      revision &&
      (revision.vehicleId !== vehicle.vehicleId || revision.branchId !== vehicle.branchId)
    ) {
      return null;
    }
  }
  if (
    draft?.id !== (vehicle.revisionId ?? undefined) ||
    (draft && draft.version !== vehicle.version)
  ) {
    return null;
  }
  let publication: ListingWorkspace["publication"] = null;
  if (value.publication !== null) {
    const published = value.publication;
    if (
      !isRecord(published) ||
      !approvedRevision ||
      !isUuid(published.id) ||
      !isUuid(published.signedRevisionId) ||
      published.vehicleId !== vehicle.vehicleId ||
      published.branchId !== vehicle.branchId ||
      published.revisionId !== approvedRevision?.id ||
      published.id !== vehicle.publicationId ||
      !date(published.recordedAt)
    ) {
      return null;
    }
    publication = {
      id: published.id,
      vehicleId: vehicle.vehicleId,
      branchId: vehicle.branchId,
      revisionId: approvedRevision.id,
      signedRevisionId: published.signedRevisionId,
      recordedAt: published.recordedAt,
    };
  }
  if (
    Boolean(publication) !== Boolean(vehicle.publicationId) ||
    approvedRevision?.id !== (vehicle.publishedRevisionId ?? undefined)
  ) {
    return null;
  }
  let withdrawal: ListingWorkspace["withdrawal"] = null;
  if (value.withdrawal !== null) {
    const withdrawn = value.withdrawal;
    if (
      !isRecord(withdrawn) ||
      !publication ||
      withdrawn.publicationId !== publication.id ||
      withdrawn.vehicleId !== vehicle.vehicleId ||
      withdrawn.branchId !== vehicle.branchId ||
      typeof withdrawn.reason !== "string" ||
      !withdrawn.reason.trim() ||
      !date(withdrawn.recordedAt)
    ) {
      return null;
    }
    withdrawal = {
      publicationId: publication.id,
      vehicleId: vehicle.vehicleId,
      branchId: vehicle.branchId,
      reason: withdrawn.reason,
      recordedAt: withdrawn.recordedAt,
    };
  }
  let currentSignedReport: ListingWorkspace["currentSignedReport"] = null;
  if (value.currentSignedReport !== null) {
    const report = value.currentSignedReport;
    if (
      !isRecord(report) ||
      !isUuid(report.inspectionId) ||
      !Number.isSafeInteger(report.revision) ||
      Number(report.revision) < 1
    ) {
      return null;
    }
    currentSignedReport = { inspectionId: report.inspectionId, revision: Number(report.revision) };
  }
  if (
    Boolean(withdrawal) !== vehicle.isWithdrawn ||
    (value.currentSignedRevisionId !== null && !isUuid(value.currentSignedRevisionId)) ||
    Boolean(value.currentSignedRevisionId) !== Boolean(currentSignedReport)
  ) {
    return null;
  }
  return {
    vehicle,
    draft,
    approvedRevision,
    publication,
    withdrawal,
    currentSignedRevisionId: value.currentSignedRevisionId as string | null,
    currentSignedReport,
  };
}
