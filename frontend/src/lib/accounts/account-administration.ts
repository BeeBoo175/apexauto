import { isRecord, isUuid } from "@/lib/auth/contracts";

export type StaffAccountCommandKind = "deactivations" | "reactivations" | "transfers";

export interface AccountBlocker {
  type: string;
  reference: string;
  status: string;
  branchName: string;
  managers: string;
}

export const accountBlockersPath = (id: string) => `/api/staff-accounts/${id}/blockers`;

export function parseBlockers(value: unknown): AccountBlocker[] | null {
  if (!isRecord(value) || !Array.isArray(value.blockers)) {
    return null;
  }
  const items = value.blockers.map((item) =>
    isRecord(item) &&
    typeof item.type === "string" &&
    typeof item.reference === "string" &&
    typeof item.status === "string" &&
    typeof item.branchName === "string" &&
    typeof item.managers === "string"
      ? (item as unknown as AccountBlocker)
      : null,
  );
  return items.every((item) => item !== null) ? items : null;
}

export interface StaffAccountChangeDraft {
  operationId: string;
  reason: string;
  branchId: string;
}

export const emptyAccountChange = (): StaffAccountChangeDraft => ({
  operationId: crypto.randomUUID(),
  reason: "",
  branchId: "",
});

const reasonOf = (body: Record<string, unknown>) =>
  typeof body.reason === "string" ? body.reason.trim() : "";

// Deactivating and transferring need a reason; reactivating records none. A transfer also names the branch.
export function shapeAccountCommand(kind: StaffAccountCommandKind, body: Record<string, unknown>) {
  if (!isUuid(body.operationId)) {
    return null;
  }
  if (kind === "reactivations") {
    return { operationId: body.operationId };
  }
  const reason = reasonOf(body);
  if (reason.length === 0 || reason.length > 1000) {
    return null;
  }
  if (kind === "deactivations") {
    return { operationId: body.operationId, reason };
  }
  return isUuid(body.branchId)
    ? { operationId: body.operationId, reason, branchId: body.branchId }
    : null;
}

const commandConflicts: Record<StaffAccountCommandKind, string> = {
  deactivations:
    "This account cannot be deactivated: it is already deactivated, it is your own, or it is the last active Administrator.",
  reactivations: "This account is not deactivated, or its branch is no longer active.",
  transfers: "This account cannot move to that branch. Refresh and choose an active branch.",
};

// The blocked conflict lists open work instead of a command-specific eligibility failure.
export function staffAccountConflict(kind: StaffAccountCommandKind, code: unknown) {
  return code === "blocked"
    ? "This employee still owns open work. Ask the branch manager to resolve it first."
    : commandConflicts[kind];
}
