import { codedConflicts } from "@/lib/api";
import { isRecord, isUtcInstant, isUuid } from "@/lib/auth/contracts";

export const CUSTOMER_CLOSURE_PATH = "/api/account/closure";
export const CUSTOMER_CLOSURE_BLOCKERS_PATH = "/api/account/closure/blockers";

// A record that still needs the customer, with the plain-words step that resolves it.
export interface ClosureBlocker {
  type: string;
  reference: string;
  status: string;
  branchName: string;
  resolution: string;
}

// One group of records that survives closure and what ends its retention.
export interface RetainedCategory {
  name: string;
  detail: string;
  endsWhen: string;
}

export interface ClosurePreview {
  blockers: ClosureBlocker[];
  retained: RetainedCategory[];
}

export interface ClosureReceipt {
  receiptId: string;
  closedAt: string;
  fileRemovalPending: boolean;
  retained: RetainedCategory[];
}

export interface ClosureDraft {
  operationId: string;
  password: string;
}

function parseBlocker(value: unknown): ClosureBlocker | null {
  return isRecord(value) &&
    typeof value.type === "string" &&
    typeof value.reference === "string" &&
    typeof value.status === "string" &&
    typeof value.branchName === "string" &&
    typeof value.resolution === "string"
    ? (value as unknown as ClosureBlocker)
    : null;
}

function parseRetained(value: unknown): RetainedCategory[] | null {
  if (!Array.isArray(value)) {
    return null;
  }
  const items = value.map((item) =>
    isRecord(item) &&
    typeof item.name === "string" &&
    typeof item.detail === "string" &&
    typeof item.endsWhen === "string"
      ? (item as unknown as RetainedCategory)
      : null,
  );
  return items.every((item) => item !== null) ? items : null;
}

export function parseClosurePreview(value: unknown): ClosurePreview | null {
  if (!isRecord(value) || !Array.isArray(value.blockers)) {
    return null;
  }
  const blockers = value.blockers.map(parseBlocker);
  const retained = parseRetained(value.retained);
  return retained && blockers.every((item) => item !== null) ? { blockers, retained } : null;
}

export function parseClosureReceipt(value: unknown): ClosureReceipt | null {
  if (
    !isRecord(value) ||
    !isUuid(value.receiptId) ||
    !isUtcInstant(value.closedAt) ||
    typeof value.fileRemovalPending !== "boolean"
  ) {
    return null;
  }
  const retained = parseRetained(value.retained);
  return retained
    ? {
        receiptId: value.receiptId,
        closedAt: value.closedAt,
        fileRemovalPending: value.fileRemovalPending,
        retained,
      }
    : null;
}

// Outcomes every password-confirmed account closure or erasure can return.
export const passwordConfirmationMessages = {
  "wrong-password": "That password is not correct. Nothing was changed.",
  "session-changed": "Your sign-in changed while you confirmed. Nothing was changed; try again.",
  "retry-later": "The service was busy. Nothing was changed; confirm again in a moment.",
};

const conflictMessages: Record<string, string> = {
  ...passwordConfirmationMessages,
  blocked: "Some records still need you. Resolve them first, then try again.",
  "protected-sample":
    "This is a published sample account and stays available to every visitor, so it cannot be closed.",
};

export const closureConflict = codedConflicts(conflictMessages);

// The closure body is exactly the operation id and the password, so a replay is byte-identical.
export function parseClosureRequest(value: unknown): ClosureDraft | null {
  return isRecord(value) &&
    isUuid(value.operationId) &&
    typeof value.password === "string" &&
    value.password.length > 0 &&
    value.password.length <= 128
    ? { operationId: value.operationId, password: value.password }
    : null;
}
