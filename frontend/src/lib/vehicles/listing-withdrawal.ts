import { isRecord, isUuid } from "@/lib/auth/contracts";
import type { ListingWorkspace } from "./listing-workspace";

export type ListingWithdrawalReceipt = NonNullable<ListingWorkspace["withdrawal"]> & {
  id: string;
  actorId: string;
};

export function listingWithdrawalReason(value: string): string | null {
  const reason = value.trim();
  return reason && reason.length <= 1000 && !/\p{Cc}/u.test(value) ? reason : null;
}

export function parseListingWithdrawalReceipt(value: unknown): ListingWithdrawalReceipt | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !isUuid(value.publicationId) ||
    !isUuid(value.vehicleId) ||
    !isUuid(value.branchId) ||
    !isUuid(value.actorId) ||
    typeof value.reason !== "string" ||
    listingWithdrawalReason(value.reason) !== value.reason ||
    typeof value.recordedAt !== "string" ||
    !/(?:Z|\+00:00)$/.test(value.recordedAt) ||
    !Number.isFinite(Date.parse(value.recordedAt))
  ) {
    return null;
  }
  return {
    id: value.id,
    publicationId: value.publicationId,
    vehicleId: value.vehicleId,
    branchId: value.branchId,
    actorId: value.actorId,
    reason: value.reason,
    recordedAt: value.recordedAt,
  };
}

export function parseListingWithdrawal(value: unknown): ListingWithdrawalReceipt | null {
  if (!isRecord(value) || (value.status !== 0 && value.status !== "Succeeded")) {
    return null;
  }
  return parseListingWithdrawalReceipt(value.withdrawal);
}

export function matchesListingWithdrawal(receipt: ListingWithdrawalReceipt, body: unknown) {
  return (
    isRecord(body) &&
    receipt.id === body.operationId &&
    receipt.publicationId === body.publicationId &&
    receipt.reason === body.reason
  );
}
