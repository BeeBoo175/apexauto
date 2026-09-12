import type { ApiConflict } from "@/lib/api";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { parseBlockers } from "./account-administration";
import {
  parseClosurePreview,
  type ClosureBlocker,
  type ClosurePreview,
  type RetainedCategory,
} from "./customer-closure";

export type ErasureKind = "customer" | "staff";

export interface ErasureDraft {
  operationId: string;
  reason: string;
  password: string;
}

export const erasurePreviewPath = (kind: ErasureKind, id: string) =>
  kind === "customer"
    ? `/api/accounts/${id}/erasure-blockers`
    : `/api/staff-accounts/${id}/blockers`;

export const erasurePath = (kind: ErasureKind, id: string) =>
  kind === "customer" ? `/api/accounts/${id}/erasures` : `/api/staff-accounts/${id}/erasures`;

function staffResolution(managers: string) {
  return managers === "No active branch manager"
    ? "No branch manager is active for this branch; assign one first."
    : `Ask the branch manager (${managers}) to finish or reassign it.`;
}

// Customers and staff share one dialog, so a staff blocker is shown in the customer blocker's shape.
export function parseErasurePreview(kind: ErasureKind, value: unknown): ClosurePreview | null {
  if (kind === "customer") {
    return parseClosurePreview(value);
  }
  const staff = parseBlockers(value);
  const customerShape = parseClosurePreview({
    blockers: [],
    retained: isRecord(value) ? value.retained : null,
  });
  if (!staff || !customerShape) {
    return null;
  }
  const blockers: ClosureBlocker[] = staff.map((item) => ({
    type: item.type,
    reference: item.reference,
    status: item.status,
    branchName: item.branchName,
    resolution: staffResolution(item.managers),
  }));
  return { blockers, retained: customerShape.retained as RetainedCategory[] };
}

// The body is the operation id, a trimmed reason and the Administrator's own password, so a replay is byte-identical.
export function parseErasureRequest(value: unknown): ErasureDraft | null {
  if (!isRecord(value) || !isUuid(value.operationId) || typeof value.reason !== "string") {
    return null;
  }
  const reason = value.reason.trim();
  return reason.length > 0 &&
    reason.length <= 1000 &&
    typeof value.password === "string" &&
    value.password.length > 0 &&
    value.password.length <= 128
    ? { operationId: value.operationId, reason, password: value.password }
    : null;
}

const conflictMessages: Record<string, string> = {
  "wrong-password": "That password is not correct. Nothing was changed.",
  blocked: "Open records still need resolving. Resolve them first, then try again.",
  protected:
    "This account cannot be erased: it is a published sample account, your own account or the last active Administrator.",
  conflict:
    "This operation was already used for a different action. Reopen the dialog and try again.",
  "session-changed": "Your sign-in changed while you confirmed. Nothing was changed; try again.",
  "retry-later": "The service was busy. Nothing was changed; confirm again in a moment.",
};

export function erasureConflict(code: unknown): ApiConflict | undefined {
  return typeof code === "string" && Object.hasOwn(conflictMessages, code)
    ? { code, message: conflictMessages[code] }
    : undefined;
}
