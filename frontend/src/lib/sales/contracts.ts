import type { CurrentActor } from "@/lib/auth/actor";

export const saleStatuses = [
  "Open",
  "Handling",
  "PendingApproval",
  "Approved",
  "Expired",
  "Cancelled",
] as const;

export type SaleStatus = (typeof saleStatuses)[number];

export const saleStatusLabels: Record<SaleStatus, string> = {
  Open: "Open",
  Handling: "Being handled",
  PendingApproval: "Needs approval",
  Approved: "Approved",
  Expired: "Expired",
  Cancelled: "Cancelled",
};

// The sale as it stands, or as it stood after one action: the same shape for the live record and its history.
export interface SaleSnapshot {
  status: SaleStatus;
  agentId: string | null;
  claimedAt: string | null;
  dueAt: string | null;
  pausedSeconds: number | null;
  orderNumber: string | null;
  promoCode: string | null;
  basePrice: number;
  roadTax: number;
  processingFee: number;
  sstRate: number;
  serviceTax: number;
  promoDiscount: number;
  negotiatedDiscount: number;
  warrantyPackageId: string | null;
  warrantyName: string | null;
  warrantyPrice: number;
  depositCredit: number;
  tradeInAppointmentId: string | null;
  tradeInLabel: string | null;
  tradeInAllowance: number;
  balancePayable: number;
  submittedAt: string | null;
  discountLimitAtSubmit: number | null;
  decidedById: string | null;
  decidedAt: string | null;
  closedAt: string | null;
  version: number;
}

export interface SaleContext {
  reservationNumber: string;
  registration: string;
  vehicle: string;
  vin: string;
  mileageKilometres: number;
  customer: string;
  customerMobile: string;
  customerEmail: string;
  branch: string;
  reservedAt: string;
  warrantyMonths: number | null;
  warrantyMileageCapKm: number | null;
  tradeInMinimum: number | null;
  tradeInMaximum: number | null;
}

// What the reading account may do next; the server decides it so every surface offers the same actions.
export interface SaleActions {
  claim: boolean;
  assign: boolean;
  release: boolean;
  saveTerms: boolean;
  submit: boolean;
  decide: boolean;
  cancel: boolean;
}

export interface Sale {
  id: string;
  sale: SaleSnapshot;
  agent: string | null;
  decidedBy: string | null;
  createdAt: string;
  updatedAt: string;
  context: SaleContext;
  actions: SaleActions;
}

export interface SaleReceipt {
  operationId: string;
  saleId: string;
  kind: string;
  version: number;
  status: SaleStatus;
  recordedAt: string;
}

export interface WarrantyOption {
  id: string;
  name: string;
  durationMonths: number;
  mileageCapKm: number;
  price: number;
  minimumGrade: string;
}

export interface TradeInOption {
  appointmentId: string;
  label: string;
  minimum: number;
  maximum: number;
}

export interface SaleOptions {
  warranties: WarrantyOption[];
  tradeIns: TradeInOption[];
}

export interface SaleAgent {
  id: string;
  email: string;
}

// One role-aware page: agents work from the queue, managers from the table.
export function hasSalesAccess(actor: CurrentActor) {
  return (
    actor.role === "SalesAgent" || actor.role === "BranchManager" || actor.role === "GeneralManager"
  );
}

export const isSalesManager = (actor: CurrentActor) =>
  actor.role === "BranchManager" || actor.role === "GeneralManager";

// One row in an agent's queue or a manager's table, read from the sale's current columns.
export interface SaleSummary {
  id: string;
  reservationNumber: string;
  orderNumber: string | null;
  status: SaleStatus;
  registration: string;
  manufactureYear: number;
  make: string;
  model: string;
  trim: string;
  customer: string;
  agent: string | null;
  branch: string;
  dueAt: string | null;
  pausedSeconds: number | null;
  claimedAt: string | null;
  negotiatedDiscount: number;
  discountLimitAtSubmit: number | null;
  balancePayable: number;
  submittedAt: string | null;
  decidedAt: string | null;
  closedAt: string | null;
  outcome: string | null;
  version: number;
  updatedAt: string;
}

// The sale's own reference: its purchase order once submitted, before that its reservation number.
export const saleReference = (row: Pick<SaleSummary, "orderNumber" | "reservationNumber">) =>
  row.orderNumber ?? row.reservationNumber;

export function remainingHoldSeconds(
  row: Pick<SaleSummary, "dueAt" | "pausedSeconds">,
  now: number,
) {
  return (
    row.pausedSeconds ?? Math.max(0, Math.floor((Date.parse(row.dueAt ?? "") - now) / 1000) || 0)
  );
}

export const saleDiscountOverLimit = (
  row: Pick<SaleSummary, "discountLimitAtSubmit" | "negotiatedDiscount">,
) => row.discountLimitAtSubmit !== null && row.negotiatedDiscount > row.discountLimitAtSubmit;

export type QueueView = "ToClaim" | "Handling" | "Waiting" | "Approved";

export const queueViews: readonly QueueView[] = ["Handling", "Waiting", "Approved", "ToClaim"];

export const queueViewLabels: Record<QueueView, string> = {
  Handling: "Sales in progress",
  Waiting: "Awaiting approval",
  Approved: "Approved sales",
  ToClaim: "Holds to claim",
};

export const queueViewTabs: Record<QueueView, string> = {
  Handling: "Handling",
  Waiting: "Waiting",
  Approved: "Approved",
  ToClaim: "To claim",
};

// The `view` query values, so a reloaded or shared address opens the same tab.
export const queueViewQueries: Record<QueueView, string> = {
  Handling: "handling",
  Waiting: "waiting",
  Approved: "approved",
  ToClaim: "claim",
};

export const queueCountKeys = ["toClaim", "handling", "waiting", "approved"] as const;

export type QueueCounts = Record<(typeof queueCountKeys)[number], number>;

export const queueCountOf: Record<QueueView, keyof QueueCounts> = {
  ToClaim: "toClaim",
  Handling: "handling",
  Waiting: "waiting",
  Approved: "approved",
};

// The tab a sale sits in for its agent, which a search result shows beside it.
export const queueViewOf: Partial<Record<SaleStatus, QueueView>> = {
  Open: "ToClaim",
  Handling: "Handling",
  PendingApproval: "Waiting",
  Approved: "Approved",
};

export interface SalesQueue {
  items: SaleSummary[];
  total: number;
  page: number;
  pageSize: number;
  counts: QueueCounts;
}

export const tableViews = [
  { value: "NeedsApproval", label: "Needs approval", count: "needsApproval" },
  { value: "Open", label: "Open", count: "open" },
  { value: "BeingHandled", label: "Being handled", count: "beingHandled" },
  { value: "Approved", label: "Approved", count: "approved" },
  { value: "History", label: "History", count: "history" },
] as const;

export type TableView = (typeof tableViews)[number]["value"];

export const tableCountKeys = tableViews.map((view) => view.count);

export type TableCounts = Record<(typeof tableViews)[number]["count"], number>;

export interface SalesTablePage {
  items: SaleSummary[];
  total: number;
  page: number;
  pageSize: number;
  counts: TableCounts;
  view: TableView;
}

export const tableSorts = [
  "reference",
  "registration",
  "vehicle",
  "customer",
  "agent",
  "branch",
  "status",
  "discount",
  "timeLeft",
  "balance",
] as const;

export const tableTextKeys = [
  "search",
  "agent",
  "branch",
  "outcome",
  "minimumDiscount",
  "maximumDiscount",
  "minimumBalance",
  "maximumBalance",
  "registration",
  "vehicle",
  "customer",
  "reference",
  "minimumHoursLeft",
  "maximumHoursLeft",
] as const;

export interface SalesTableFilters extends Record<(typeof tableTextKeys)[number], string> {
  // Empty until the server has chosen the first tab with work.
  view: TableView | "";
  pageSize: number;
  sort: string;
  direction: "asc" | "desc";
}

// How long ago the agent took the sale, so a claim left sitting is easy to see.
export function claimAge(claimedAt: string | null, now: number) {
  if (!claimedAt) {
    return "just now";
  }
  const days = Math.floor((now - Date.parse(claimedAt)) / 86_400_000);
  if (days <= 0) {
    return "today";
  }
  return days === 1 ? "1 day ago" : `${days} days ago`;
}

// The commands a sale accepts; the browser names them in paths and the relay allows exactly these.
export const saleCommands = [
  "claims",
  "assignments",
  "releases",
  "term-revisions",
  "submissions",
  "approvals",
  "rejections",
  "cancellations",
] as const;

export type SaleCommand = (typeof saleCommands)[number];
