import { parsePage } from "@/lib/api";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { formatCurrency } from "@/lib/currency-format";

export type HistorySource = "Hold" | "Deal" | "Refund";

export interface HoldHistoryChange {
  field: string;
  before: string | null;
  after: string | null;
}

// One retained receipt for a hold or its deal; the id is the receipt an operation can be traced by.
export interface HoldHistoryItem {
  id: string;
  source: HistorySource;
  kind: string;
  recordedAtUtc: string;
  actorId: string | null;
  actor: string;
  agent: string | null;
  previousAgent: string | null;
  reason: string | null;
  fromStatus: string | null;
  toStatus: string | null;
  changes: HoldHistoryChange[];
}

const sources: readonly string[] = ["Hold", "Deal", "Refund"];
const nullableText = (value: unknown) =>
  value === null || (typeof value === "string" && value !== "");
const utc = (value: unknown): value is string =>
  typeof value === "string" && /(?:Z|\+00:00)$/.test(value) && Number.isFinite(Date.parse(value));

function parseChange(value: unknown): HoldHistoryChange | null {
  return isRecord(value) &&
    typeof value.field === "string" &&
    value.field !== "" &&
    (value.before === null || typeof value.before === "string") &&
    (value.after === null || typeof value.after === "string")
    ? (value as unknown as HoldHistoryChange)
    : null;
}

export function parseHoldHistoryItem(value: unknown): HoldHistoryItem | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    typeof value.source !== "string" ||
    !sources.includes(value.source) ||
    typeof value.kind !== "string" ||
    value.kind === "" ||
    !utc(value.recordedAtUtc) ||
    !(value.actorId === null || isUuid(value.actorId)) ||
    typeof value.actor !== "string" ||
    !nullableText(value.agent) ||
    !nullableText(value.previousAgent) ||
    !nullableText(value.reason) ||
    !nullableText(value.fromStatus) ||
    !nullableText(value.toStatus) ||
    !Array.isArray(value.changes)
  ) {
    return null;
  }
  const changes = value.changes.map(parseChange);
  return changes.every((change) => change !== null)
    ? ({ ...value, changes } as unknown as HoldHistoryItem)
    : null;
}

// Shaped for the shared staff history reader, which pages by `totalCount`.
export interface HoldHistory {
  items: HoldHistoryItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export function parseHoldHistory(value: unknown): HoldHistory | null {
  // The page carries `totalCount`, as the workshop histories do; the shared page reader calls it `total`.
  const page = parsePage(
    isRecord(value) ? { ...value, total: value.totalCount } : value,
    parseHoldHistoryItem,
  );
  if (!page || new Set(page.items.map((item) => item.id)).size !== page.items.length) {
    return null;
  }
  return { items: page.items, totalCount: page.total, page: page.page, pageSize: page.pageSize };
}

export const appendHoldHistory = (previous: HoldHistory, next: HoldHistory): HoldHistory => {
  const items = new Map([...previous.items, ...next.items].map((item) => [item.id, item]));
  return { ...next, items: [...items.values()] };
};

const refundCauses: Record<string, string> = {
  CustomerCancelled: "Customer cancelled the hold",
  StaffCancelled: "Manager cancelled the hold",
  Expired: "The hold expired",
  Withdrawn: "The listing was withdrawn",
  Reinspection: "The vehicle needs reinspection",
  DealCancelled: "The deal was cancelled",
};

const dealEvents: Record<string, string> = {
  Revised: "Terms changed",
  Submitted: "Deal submitted",
  Approved: "Deal approved",
  Rejected: "Deal returned to draft",
  Cancelled: "Deal cancelled",
};

// The plain-language line for a receipt; staff are named by email.
export function historyTitle(item: HoldHistoryItem) {
  if (item.source === "Refund") {
    return `Deposit refunded · ${refundCauses[item.kind] ?? item.kind}`;
  }
  if (item.source === "Deal") {
    return dealEvents[item.kind] ?? item.kind;
  }
  switch (item.kind) {
    case "Claimed":
      return "Claimed the hold";
    case "Assigned":
      return `Assigned the hold to ${item.agent}`;
    case "Reassigned":
      return `Moved the hold from ${item.previousAgent} to ${item.agent}`;
    case "Released":
      return "Released the claim";
    default:
      return item.kind;
  }
}

const money = (value: string | null) => (value === null ? "None" : formatCurrency(Number(value)));

// "Negotiated discount MYR 0.00 → MYR 300.00": what a revision changed.
export function historyChange(change: HoldHistoryChange) {
  switch (change.field) {
    case "NegotiatedDiscount":
      return `Negotiated discount ${money(change.before)} → ${money(change.after)}`;
    case "TradeInAllowance":
      return `Trade-in allowance ${money(change.before)} → ${money(change.after)}`;
    case "Warranty":
      return `Warranty ${change.before ?? "None"} → ${change.after ?? "None"}`;
    default:
      return `${change.field} ${change.before ?? "None"} → ${change.after ?? "None"}`;
  }
}
