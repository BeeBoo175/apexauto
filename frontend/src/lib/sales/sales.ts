import { parseCountedPage } from "@/lib/api";
import type { CurrentActor } from "@/lib/auth/actor";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { isMyr } from "@/lib/currency-input";
import { DEFAULT_PAGE_SIZE } from "@/lib/pagination";
import type { SearchParams } from "@/lib/query";

export type SalesView = "ToClaim" | "Handling" | "Waiting";

export const salesViews: readonly SalesView[] = ["Handling", "Waiting", "ToClaim"];

export const salesViewLabels: Record<SalesView, string> = {
  Handling: "Deals in progress",
  Waiting: "Awaiting approval",
  ToClaim: "Holds to claim",
};

export const salesViewTabs: Record<SalesView, string> = {
  Handling: "Handling",
  Waiting: "Waiting",
  ToClaim: "To claim",
};

// The `view` query values, so a reloaded or shared address opens the same tab.
export const salesViewQueries: Record<SalesView, string> = {
  Handling: "handling",
  Waiting: "waiting",
  ToClaim: "claim",
};

export type SalesSection = "customer" | "deal" | "history";

export const salesSections: readonly SalesSection[] = ["customer", "deal", "history"];

export const salesSectionLabels: Record<SalesSection, string> = {
  customer: "Customer & car",
  deal: "Deal",
  history: "History",
};

export interface SalesQueueItem {
  reservationId: string;
  dealId: string | null;
  registration: string;
  vehicle: string;
  customer: string;
  holdCreatedAtUtc: string;
  expiresAtUtc: string | null;
  frozenRemainingSeconds: number | null;
  claimedAtUtc: string | null;
  dealStatus: string | null;
  nextStep: string | null;
  balancePayable: number | null;
  dealVersion: number | null;
}

export const salesCountKeys = ["toClaim", "handling", "waiting"] as const;

export type SalesCounts = Record<(typeof salesCountKeys)[number], number>;

export interface SalesQueue {
  items: SalesQueueItem[];
  total: number;
  page: number;
  pageSize: number;
  counts: SalesCounts;
}

// Agents work holds and deals from the sales workspace; managers keep the tables.
export function hasSalesAccess(actor: CurrentActor) {
  return actor.role === "SalesAgent";
}

const text = (value: unknown): value is string => typeof value === "string" && value.length > 0;
const utc = (value: unknown): value is string =>
  typeof value === "string" && /(?:Z|\+00:00)$/.test(value) && Number.isFinite(Date.parse(value));
const nullable = (check: (value: unknown) => boolean) => (value: unknown) =>
  value === null || check(value);
const seconds = (value: unknown): value is number =>
  Number.isSafeInteger(value) && Number(value) > 0;

export function parseSalesQueueItem(value: unknown): SalesQueueItem | null {
  if (
    !isRecord(value) ||
    !isUuid(value.reservationId) ||
    !nullable(isUuid)(value.dealId) ||
    !text(value.registration) ||
    !text(value.vehicle) ||
    !text(value.customer) ||
    !utc(value.holdCreatedAtUtc) ||
    !nullable(utc)(value.expiresAtUtc) ||
    !nullable(seconds)(value.frozenRemainingSeconds) ||
    !nullable(utc)(value.claimedAtUtc) ||
    !nullable(text)(value.dealStatus) ||
    !nullable(text)(value.nextStep) ||
    !nullable(isMyr)(value.balancePayable) ||
    !nullable(seconds)(value.dealVersion)
  ) {
    return null;
  }
  return value as unknown as SalesQueueItem;
}

export function parseSalesQueue(value: unknown): SalesQueue | null {
  const page = parseCountedPage(value, parseSalesQueueItem, salesCountKeys);
  if (
    !page?.counts ||
    new Set(page.items.map((item) => item.reservationId)).size !== page.items.length
  ) {
    return null;
  }
  return { ...page, counts: page.counts };
}

export function salesQueueParams(parameters: SearchParams): SalesView | null {
  return salesViews.find((view) => salesViewQueries[view] === parameters.view) ?? null;
}

export function salesQuery(view: SalesView, page = 1) {
  return new URLSearchParams({
    view,
    page: String(page),
    pageSize: String(DEFAULT_PAGE_SIZE),
  }).toString();
}

export function salesSelection(parameters: SearchParams) {
  const section = salesSections.find((item) => item === parameters.section);
  return {
    dealId: isUuid(parameters.deal) ? parameters.deal : "",
    section: section ?? "customer",
  };
}

// One tab opens at its first screen: the deals in progress when there are any, otherwise the holds to claim.
export function firstView(requested: SalesView | null, counts: SalesCounts | undefined): SalesView {
  if (requested) {
    return requested;
  }
  return counts && counts.handling === 0 ? "ToClaim" : "Handling";
}

// How long ago an agent took the hold, so a claim left sitting is easy to see.
export function claimAge(claimedAtUtc: string | null, now: number) {
  if (!claimedAtUtc) {
    return "just now";
  }
  const days = Math.floor((now - Date.parse(claimedAtUtc)) / 86_400_000);
  if (days <= 0) {
    return "today";
  }
  return days === 1 ? "1 day ago" : `${days} days ago`;
}

export const holdsPath = (reservationId: string) => `/api/staff/reservations/${reservationId}`;

export const salesQueuePath = "/api/staff/sales/queue";
