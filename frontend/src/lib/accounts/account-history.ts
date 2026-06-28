import { parsePage } from "@/lib/api";
import { parseActor, type CurrentActor } from "@/lib/auth/actor";
import { isRecord, isUtcInstant, isUuid } from "@/lib/auth/contracts";

export const accountEventTitles: Record<string, string> = {
  UsernameChanged: "Username changed",
  PictureRemoved: "Picture removed",
  ProfileEdited: "Profile edited",
  EmailChanged: "Email changed",
  BranchTransferred: "Branch transferred",
  Suspended: "Account suspended",
  SuspensionLifted: "Suspension lifted",
  Deactivated: "Account deactivated",
  Reactivated: "Account reactivated",
};

export interface AccountEventChange {
  field: string;
  before: string | null;
  after: string | null;
}

export interface AccountEvent {
  id: string;
  kind: string;
  recordedAt: string;
  actorId: string;
  actor: string;
  reason: string | null;
  changes: AccountEventChange[];
}

export interface AccountHistory {
  actor: CurrentActor;
  accountId: string;
  items: AccountEvent[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export const accountHistoryPath = (id: string) => `/api/staff-accounts/${id}/history`;

const optionalText = (value: unknown): value is string | null =>
  value === null || typeof value === "string";

function parseChange(value: unknown): AccountEventChange | null {
  return isRecord(value) &&
    typeof value.field === "string" &&
    optionalText(value.before) &&
    optionalText(value.after)
    ? (value as unknown as AccountEventChange)
    : null;
}

function parseEvent(value: unknown): AccountEvent | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    typeof value.kind !== "string" ||
    !Object.hasOwn(accountEventTitles, value.kind) ||
    !isUtcInstant(value.recordedAt) ||
    !isUuid(value.actorId) ||
    typeof value.actor !== "string" ||
    !value.actor.trim() ||
    !optionalText(value.reason) ||
    !Array.isArray(value.changes)
  ) {
    return null;
  }
  const changes = value.changes.map(parseChange);
  return changes.every((change) => change !== null)
    ? ({ ...value, changes } as unknown as AccountEvent)
    : null;
}

// The page carries `totalCount`, as the other histories do; the shared page reader calls it `total`.
export function parseAccountHistory(value: unknown): AccountHistory | null {
  const actor = isRecord(value) ? parseActor(value.actor) : null;
  const page = parsePage(
    isRecord(value) ? { ...value, total: value.totalCount } : value,
    parseEvent,
  );
  if (
    !actor ||
    !isRecord(value) ||
    !isUuid(value.accountId) ||
    !page ||
    new Set(page.items.map((item) => item.id)).size !== page.items.length
  ) {
    return null;
  }
  return {
    actor,
    accountId: value.accountId,
    items: page.items,
    totalCount: page.total,
    page: page.page,
    pageSize: page.pageSize,
  };
}

export const appendAccountHistory = (
  previous: AccountHistory,
  next: AccountHistory,
): AccountHistory => {
  const items = new Map([...previous.items, ...next.items].map((item) => [item.id, item]));
  return { ...next, items: [...items.values()] };
};

// One line per changed field, such as "Access: Active to Suspended".
export const accountChangeText = (change: AccountEventChange) =>
  change.before !== null && change.after !== null
    ? `${change.field}: ${change.before} to ${change.after}`
    : `${change.field}: ${change.after ?? change.before ?? ""}`;
