import { isRecord, isUuid } from "@/lib/auth/contracts";
import type { ListingWorkspace } from "./listing-workspace";

export type ListingPublicationReceipt = NonNullable<ListingWorkspace["publication"]> & {
  actorId: string;
};

// Recovery keeps the captured operation available even outside the current Publication tab.
export function listingPublicationAction({
  current,
  canApprove,
  withdrawalReason,
  approval,
  withdrawal,
}: {
  current: boolean;
  canApprove: boolean;
  withdrawalReason: string;
  approval: { busy: boolean; uncertain: boolean };
  withdrawal: { busy: boolean; uncertain: boolean };
}): "approve" | "withdraw" | null {
  if (approval.busy || approval.uncertain) {
    return "approve";
  }
  if (withdrawal.busy || withdrawal.uncertain) {
    return "withdraw";
  }
  if (!current) {
    return null;
  }
  if (withdrawalReason) {
    return "withdraw";
  }
  return canApprove ? "approve" : null;
}

export function parseListingPublicationReceipt(value: unknown): ListingPublicationReceipt | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !isUuid(value.vehicleId) ||
    !isUuid(value.branchId) ||
    !isUuid(value.revisionId) ||
    !isUuid(value.signedRevisionId) ||
    !isUuid(value.actorId) ||
    typeof value.recordedAt !== "string" ||
    !/(?:Z|\+00:00)$/.test(value.recordedAt) ||
    !Number.isFinite(Date.parse(value.recordedAt))
  ) {
    return null;
  }
  return {
    id: value.id,
    vehicleId: value.vehicleId,
    branchId: value.branchId,
    revisionId: value.revisionId,
    signedRevisionId: value.signedRevisionId,
    actorId: value.actorId,
    recordedAt: value.recordedAt,
  };
}

export function parseListingPublication(value: unknown): ListingPublicationReceipt | null {
  if (!isRecord(value) || (value.status !== 0 && value.status !== "Succeeded")) {
    return null;
  }
  return parseListingPublicationReceipt(value.publication);
}

export function matchesListingPublication(receipt: ListingPublicationReceipt, body: unknown) {
  return (
    isRecord(body) &&
    receipt.id === body.operationId &&
    receipt.revisionId === body.revisionId &&
    receipt.signedRevisionId === body.signedRevisionId
  );
}
