import { isRecord, isUuid } from "@/lib/auth/contracts";

export interface DepositRequestCancellation {
  id: string;
}

export function parseDepositRequestCancellation(value: unknown): DepositRequestCancellation | null {
  return isRecord(value) && isUuid(value.id) ? { id: value.id } : null;
}

// The receipt must name the request that was asked to be cancelled.
export function matchesDepositRequestCancellation(
  receipt: DepositRequestCancellation,
  requestId: string,
) {
  return receipt.id === requestId;
}
