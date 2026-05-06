import { parsePage } from "@/lib/api";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { formatCurrency } from "@/lib/currency-format";
import { isMyr } from "@/lib/currency-input";
import {
  isSaleStatus,
  nullable,
  parseSaleSnapshot,
  text,
  utc,
  type SaleSnapshot,
  type SaleStatus,
} from "./sale";
import type { SaleSection } from "./sale-sections";

export interface SaleChange {
  field: string;
  before: string | null;
  after: string | null;
}

// One retained entry: what happened, who did it and when, and what it changed.
export interface SaleHistoryItem {
  id: string;
  version: number;
  kind: string;
  recordedAt: string;
  actor: string;
  agent: string | null;
  previousAgent: string | null;
  fromStatus: SaleStatus | null;
  toStatus: SaleStatus;
  reason: string | null;
  changes: SaleChange[];
  refund: number | null;
}

// Shaped for the shared staff history reader, which pages by `totalCount`.
export interface SaleHistory {
  items: SaleHistoryItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// The sale as it stood after one entry, with the version before it so changed fields can be marked.
export interface SaleVersion {
  saleId: string;
  version: number;
  kind: string;
  recordedAt: string;
  actor: string;
  agent: string | null;
  reason: string | null;
  snapshot: SaleSnapshot;
  previous: SaleSnapshot | null;
}

function parseChange(value: unknown): SaleChange | null {
  return isRecord(value) &&
    text(value.field) &&
    (value.before === null || typeof value.before === "string") &&
    (value.after === null || typeof value.after === "string")
    ? (value as unknown as SaleChange)
    : null;
}

function parseItem(value: unknown): SaleHistoryItem | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !Number.isSafeInteger(value.version) ||
    !text(value.kind) ||
    !utc(value.recordedAt) ||
    !text(value.actor) ||
    !nullable(text)(value.agent) ||
    !nullable(text)(value.previousAgent) ||
    !(value.fromStatus === null || isSaleStatus(value.fromStatus)) ||
    !isSaleStatus(value.toStatus) ||
    !nullable(text)(value.reason) ||
    !nullable(isMyr)(value.refund) ||
    !Array.isArray(value.changes)
  ) {
    return null;
  }
  const changes = value.changes.map(parseChange);
  return changes.every((change) => change !== null)
    ? ({ ...value, changes } as unknown as SaleHistoryItem)
    : null;
}

export function parseSaleHistory(value: unknown): SaleHistory | null {
  // The page carries `totalCount`, as the workshop histories do; the shared page reader calls it `total`.
  const page = parsePage(
    isRecord(value) ? { ...value, total: value.totalCount } : value,
    parseItem,
  );
  if (!page || new Set(page.items.map((item) => item.id)).size !== page.items.length) {
    return null;
  }
  return { items: page.items, totalCount: page.total, page: page.page, pageSize: page.pageSize };
}

export const appendSaleHistory = (previous: SaleHistory, next: SaleHistory): SaleHistory => {
  const items = new Map([...previous.items, ...next.items].map((item) => [item.id, item]));
  return { ...next, items: [...items.values()] };
};

export function parseSaleVersion(value: unknown): SaleVersion | null {
  if (
    !isRecord(value) ||
    !isUuid(value.saleId) ||
    !Number.isSafeInteger(value.version) ||
    Number(value.version) < 1 ||
    !text(value.kind) ||
    !utc(value.recordedAt) ||
    !text(value.actor) ||
    !nullable(text)(value.agent) ||
    !nullable(text)(value.reason)
  ) {
    return null;
  }
  const snapshot = parseSaleSnapshot(value.snapshot);
  const previous = value.previous === null ? null : parseSaleSnapshot(value.previous);
  if (!snapshot || (value.previous !== null && !previous)) {
    return null;
  }
  return { ...value, snapshot, previous } as unknown as SaleVersion;
}

// Handling actions are read on Customer & car, where the agent and timer show; everything else on Deal.
export function versionSection(kind: string): SaleSection {
  return ["Claimed", "Assigned", "Reassigned", "Released", "Expired"].includes(kind)
    ? "customer"
    : "deal";
}

const titles: Record<string, string> = {
  Created: "Reserved online",
  Claimed: "Claimed",
  Released: "Released to the queue",
  TermsSaved: "Terms saved",
  Approved: "Approved",
  Rejected: "Returned to the agent",
  Cancelled: "Cancelled",
  CustomerCancelled: "Cancelled by the customer",
  Expired: "Expired",
  Withdrawn: "Cancelled automatically",
};

// The plain-language line for an entry; staff are named by email.
export function historyTitle(
  item: Pick<SaleHistoryItem, "kind" | "toStatus" | "agent"> & { previousAgent?: string | null },
) {
  switch (item.kind) {
    case "Assigned":
      return `Assigned to ${item.agent}`;
    case "Reassigned":
      return item.previousAgent
        ? `Moved from ${item.previousAgent} to ${item.agent}`
        : `Moved to ${item.agent}`;
    case "Submitted":
      return item.toStatus === "Approved" ? "Submitted and approved" : "Submitted for approval";
    default:
      return titles[item.kind] ?? item.kind;
  }
}

const money = (value: string | null) => (value === null ? "None" : formatCurrency(Number(value)));

const changeLabels: Record<string, string> = {
  NegotiatedDiscount: "Negotiated discount",
  Warranty: "Warranty",
  TradeIn: "Trade-in",
  TradeInAllowance: "Trade-in allowance",
};

// "Negotiated discount RM 0.00 → RM 300.00": what an entry changed.
export function historyChange(change: SaleChange) {
  const label = changeLabels[change.field] ?? change.field;
  const amount = change.field === "NegotiatedDiscount" || change.field === "TradeInAllowance";
  const before = amount ? money(change.before) : (change.before ?? "None");
  const after = amount ? money(change.after) : (change.after ?? "None");
  return `${label} ${before} → ${after}`;
}

export const saleHistoryPath = (id: string) => `/api/staff/sales/${id}/history`;

export const saleVersionPath = (id: string, version: number) =>
  `/api/staff/sales/${id}/versions/${version}`;
