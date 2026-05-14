import { isRecord, isUuid } from "@/lib/auth/contracts";
import { isMyr } from "@/lib/currency-input";
import { parseCountedPage } from "@/lib/api";
import {
  saleStatuses,
  type SaleStatus,
  type SaleSnapshot,
  type SaleContext,
  type Sale,
  type SaleReceipt,
  type WarrantyOption,
  type TradeInOption,
  type SaleOptions,
  type SaleAgent,
  type SaleSummary,
  queueCountKeys,
  type SalesQueue,
  tableViews,
  tableCountKeys,
  type TableCounts,
  type SalesTablePage,
} from "./contracts";

export const text = (value: unknown): value is string => typeof value === "string" && value !== "";

export const utc = (value: unknown): value is string =>
  typeof value === "string" && /(?:Z|\+00:00)$/.test(value) && Number.isFinite(Date.parse(value));

export const nullable =
  <T>(check: (value: unknown) => value is T) =>
  (value: unknown): value is T | null =>
    value === null || check(value);

const count = (value: unknown): value is number =>
  Number.isSafeInteger(value) && Number(value) >= 0;

const version = (value: unknown): value is number =>
  Number.isSafeInteger(value) && Number(value) >= 1;

const rate = (value: unknown): value is number =>
  typeof value === "number" && Number.isFinite(value) && value >= 0 && value <= 1;
// A return or cancellation reason as the backend accepts it: trimmed, between 1 and 1000 characters.

export const isSaleReason = (value: unknown): value is string =>
  typeof value === "string" && value === value.trim() && value.length >= 1 && value.length <= 1000;

export const isSaleStatus = (value: unknown): value is SaleStatus =>
  saleStatuses.some((status) => status === value);

const money = [
  "basePrice",
  "roadTax",
  "processingFee",
  "serviceTax",
  "promoDiscount",
  "negotiatedDiscount",
  "warrantyPrice",
  "depositCredit",
  "tradeInAllowance",
  "balancePayable",
] as const;

export function parseSaleSnapshot(value: unknown): SaleSnapshot | null {
  if (
    !isRecord(value) ||
    !isSaleStatus(value.status) ||
    !nullable(isUuid)(value.agentId) ||
    !nullable(utc)(value.claimedAt) ||
    !nullable(utc)(value.dueAt) ||
    !nullable(version)(value.pausedSeconds) ||
    !nullable(text)(value.orderNumber) ||
    !nullable(text)(value.promoCode) ||
    !rate(value.sstRate) ||
    !money.every((field) => isMyr(value[field])) ||
    !nullable(isUuid)(value.warrantyPackageId) ||
    !nullable(text)(value.warrantyName) ||
    !nullable(isUuid)(value.tradeInAppointmentId) ||
    !nullable(text)(value.tradeInLabel) ||
    !nullable(utc)(value.submittedAt) ||
    !nullable(isMyr)(value.discountLimitAtSubmit) ||
    !nullable(isUuid)(value.decidedById) ||
    !nullable(utc)(value.decidedAt) ||
    !nullable(utc)(value.closedAt) ||
    !version(value.version)
  ) {
    return null;
  }
  return value as unknown as SaleSnapshot;
}

function parseContext(value: unknown): SaleContext | null {
  if (
    !isRecord(value) ||
    !text(value.reservationNumber) ||
    !text(value.registration) ||
    !text(value.vehicle) ||
    typeof value.vin !== "string" ||
    !count(value.mileageKilometres) ||
    !text(value.customer) ||
    typeof value.customerMobile !== "string" ||
    typeof value.customerEmail !== "string" ||
    !text(value.branch) ||
    !utc(value.reservedAt) ||
    !nullable(count)(value.warrantyMonths) ||
    !nullable(count)(value.warrantyMileageCapKm) ||
    !nullable(isMyr)(value.tradeInMinimum) ||
    !nullable(isMyr)(value.tradeInMaximum)
  ) {
    return null;
  }
  return value as unknown as SaleContext;
}

const actionKeys = [
  "claim",
  "assign",
  "release",
  "saveTerms",
  "submit",
  "decide",
  "cancel",
] as const;

export function parseSale(value: unknown): Sale | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !nullable(text)(value.agent) ||
    !nullable(text)(value.decidedBy) ||
    !utc(value.createdAt) ||
    !utc(value.updatedAt) ||
    !isRecord(value.actions) ||
    !actionKeys.every((key) => typeof (value.actions as Record<string, unknown>)[key] === "boolean")
  ) {
    return null;
  }
  const sale = parseSaleSnapshot(value.sale);
  const context = parseContext(value.context);
  return sale && context ? ({ ...value, sale, context } as unknown as Sale) : null;
}

export function parseSaleReceipt(value: unknown): SaleReceipt | null {
  return isRecord(value) &&
    isUuid(value.operationId) &&
    isUuid(value.saleId) &&
    text(value.kind) &&
    version(value.version) &&
    isSaleStatus(value.status) &&
    utc(value.recordedAt)
    ? (value as unknown as SaleReceipt)
    : null;
}

function parseWarranty(value: unknown): WarrantyOption | null {
  return isRecord(value) &&
    isUuid(value.id) &&
    text(value.name) &&
    count(value.durationMonths) &&
    count(value.mileageCapKm) &&
    isMyr(value.price) &&
    text(value.minimumGrade)
    ? (value as unknown as WarrantyOption)
    : null;
}

function parseTradeIn(value: unknown): TradeInOption | null {
  return isRecord(value) &&
    isUuid(value.appointmentId) &&
    text(value.label) &&
    isMyr(value.minimum) &&
    isMyr(value.maximum)
    ? (value as unknown as TradeInOption)
    : null;
}

export function parseSaleOptions(value: unknown): SaleOptions | null {
  if (!isRecord(value) || !Array.isArray(value.warranties) || !Array.isArray(value.tradeIns)) {
    return null;
  }
  const warranties = value.warranties.map(parseWarranty);
  const tradeIns = value.tradeIns.map(parseTradeIn);
  if (warranties.some((item) => item === null) || tradeIns.some((item) => item === null)) {
    return null;
  }
  return { warranties, tradeIns } as SaleOptions;
}

export function parseSaleAgents(value: unknown): SaleAgent[] | null {
  if (!Array.isArray(value)) {
    return null;
  }
  const agents = value.map((item) =>
    isRecord(item) && isUuid(item.id) && text(item.email) ? (item as unknown as SaleAgent) : null,
  );
  return agents.every((item) => item !== null) ? (agents as SaleAgent[]) : null;
}

const positive = (value: unknown): value is number =>
  Number.isSafeInteger(value) && Number(value) > 0;

export function parseSaleSummary(value: unknown): SaleSummary | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !text(value.reservationNumber) ||
    !nullable(text)(value.orderNumber) ||
    !isSaleStatus(value.status) ||
    !text(value.registration) ||
    !positive(value.manufactureYear) ||
    !text(value.make) ||
    !text(value.model) ||
    typeof value.trim !== "string" ||
    !text(value.customer) ||
    !nullable(text)(value.agent) ||
    !text(value.branch) ||
    !nullable(utc)(value.dueAt) ||
    !nullable(positive)(value.pausedSeconds) ||
    !nullable(utc)(value.claimedAt) ||
    !isMyr(value.negotiatedDiscount) ||
    !nullable(isMyr)(value.discountLimitAtSubmit) ||
    !isMyr(value.balancePayable) ||
    !nullable(utc)(value.submittedAt) ||
    !nullable(utc)(value.decidedAt) ||
    !nullable(utc)(value.closedAt) ||
    !nullable(text)(value.outcome) ||
    !utc(value.updatedAt) ||
    !positive(value.version)
  ) {
    return null;
  }
  return value as unknown as SaleSummary;
}

function uniquePage<T extends { items: SaleSummary[] }>(page: T | null) {
  return page && new Set(page.items.map((item) => item.id)).size === page.items.length
    ? page
    : null;
}

export function parseSalesQueue(value: unknown): SalesQueue | null {
  const page = uniquePage(parseCountedPage(value, parseSaleSummary, queueCountKeys));
  return page?.counts ? { ...page, counts: page.counts } : null;
}

export function parseSalesTable(value: unknown): SalesTablePage | null {
  const page = uniquePage(parseCountedPage(value, parseSaleSummary, tableCountKeys));
  const view = isRecord(value)
    ? tableViews.find((item) => item.value === value.view)?.value
    : undefined;
  return page?.counts && view ? { ...page, counts: page.counts as TableCounts, view } : null;
}
