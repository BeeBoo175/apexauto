import { handlerName } from "@/lib/account/handler-display";
import {
  customerIdOf,
  isNonNegativeInteger,
  isPositiveInteger,
  isRecord,
  isUtcInstant,
  isUuid,
} from "@/lib/auth/contracts";
import { isMyr } from "@/lib/currency-input";
import { parseCountedPage, parseItems } from "@/lib/api";
import { parsePaymentEvidence, parseHandoverEvidence } from "./completion-evidence";
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
  type DepositRequestRow,
} from "./contracts";

export const text = (value: unknown): value is string => typeof value === "string" && value !== "";

export const nullable =
  <T>(check: (value: unknown) => value is T) =>
  (value: unknown): value is T | null =>
    value === null || check(value);

const rate = (value: unknown): value is number =>
  typeof value === "number" && Number.isFinite(value) && value >= 0 && value <= 1;
// A return or cancellation reason as the backend accepts it: trimmed, between 1 and 1000 characters.

export const isSaleReason = (value: unknown): value is string =>
  typeof value === "string" &&
  value === value.trim() &&
  value.length >= 1 &&
  value.length <= 1000 &&
  !/\p{Cc}/u.test(value);

export function normalizeSaleReason(value: unknown): string | null {
  if (typeof value !== "string") {
    return null;
  }
  const reason = value.trim();
  return isSaleReason(reason) ? reason : null;
}

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
    !nullable(isUtcInstant)(value.claimedAt) ||
    !nullable(isUtcInstant)(value.dueAt) ||
    !nullable(isPositiveInteger)(value.pausedSeconds) ||
    !nullable(text)(value.orderNumber) ||
    !nullable(text)(value.promoCode) ||
    !rate(value.sstRate) ||
    !money.every((field) => isMyr(value[field])) ||
    !nullable(isUuid)(value.warrantyPackageId) ||
    !nullable(text)(value.warrantyName) ||
    !nullable(isUuid)(value.tradeInAppointmentId) ||
    !nullable(text)(value.tradeInLabel) ||
    !nullable(isUtcInstant)(value.submittedAt) ||
    !nullable(isMyr)(value.discountLimitAtSubmit) ||
    !nullable(isUuid)(value.decidedById) ||
    !nullable(isUtcInstant)(value.decidedAt) ||
    !nullable(isUtcInstant)(value.closedAt) ||
    !isPositiveInteger(value.version)
  ) {
    return null;
  }
  if (
    !nullable(isUtcInstant)(value.settledAt) ||
    !nullable(isUuid)(value.acquiredVehicleId) ||
    !nullable(isUtcInstant)(value.deliveredAt) ||
    (value.paymentEvidence !== null && !parsePaymentEvidence(value.paymentEvidence)) ||
    (value.handoverEvidence !== null && !parseHandoverEvidence(value.handoverEvidence))
  ) {
    return null;
  }
  if (
    (["Settled", "Delivered"].includes(value.status) &&
      (!value.paymentEvidence || !value.settledAt)) ||
    (value.status === "Delivered" && (!value.handoverEvidence || !value.deliveredAt))
  ) {
    return null;
  }
  return {
    ...value,
    paymentEvidence:
      value.paymentEvidence === null ? null : parsePaymentEvidence(value.paymentEvidence),
    handoverEvidence:
      value.handoverEvidence === null ? null : parseHandoverEvidence(value.handoverEvidence),
  } as SaleSnapshot;
}

function parseContext(value: unknown): SaleContext | null {
  if (
    !isRecord(value) ||
    !text(value.reservationNumber) ||
    !text(value.registration) ||
    !text(value.vehicle) ||
    typeof value.vin !== "string" ||
    !isNonNegativeInteger(value.mileageKilometres) ||
    !text(value.customer) ||
    typeof value.customerMobile !== "string" ||
    typeof value.customerEmail !== "string" ||
    !text(value.branch) ||
    !isUtcInstant(value.reservedAt) ||
    !nullable(isNonNegativeInteger)(value.warrantyMonths) ||
    !nullable(isNonNegativeInteger)(value.warrantyMileageCapKm) ||
    !nullable(isMyr)(value.tradeInMinimum) ||
    !nullable(isMyr)(value.tradeInMaximum)
  ) {
    return null;
  }
  return { ...value, customerId: customerIdOf(value) } as unknown as SaleContext;
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
    !isUtcInstant(value.createdAt) ||
    !isUtcInstant(value.updatedAt) ||
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
    isPositiveInteger(value.version) &&
    isSaleStatus(value.status) &&
    isUtcInstant(value.recordedAt)
    ? (value as unknown as SaleReceipt)
    : null;
}

function parseWarranty(value: unknown): WarrantyOption | null {
  return isRecord(value) &&
    isUuid(value.id) &&
    text(value.name) &&
    isNonNegativeInteger(value.durationMonths) &&
    isNonNegativeInteger(value.mileageCapKm) &&
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
    isRecord(item) && isUuid(item.id) && text(item.email)
      ? ({
          id: item.id,
          email: item.email,
          name: handlerName(item.name, item.email),
        } satisfies SaleAgent)
      : null,
  );
  return agents.every((item) => item !== null) ? (agents as SaleAgent[]) : null;
}

export function parseSaleSummary(value: unknown): SaleSummary | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !text(value.reservationNumber) ||
    !nullable(text)(value.orderNumber) ||
    !isSaleStatus(value.status) ||
    !text(value.registration) ||
    !isPositiveInteger(value.manufactureYear) ||
    !text(value.make) ||
    !text(value.model) ||
    typeof value.trim !== "string" ||
    !text(value.customer) ||
    !nullable(text)(value.agent) ||
    !nullable(isUuid)(value.agentId) ||
    !nullable(text)(value.agentName) ||
    !nullable(isUuid)(value.agentPictureId) ||
    (value.agent === null) !== (value.agentId === null) ||
    !text(value.branch) ||
    !nullable(isUtcInstant)(value.dueAt) ||
    !nullable(isPositiveInteger)(value.pausedSeconds) ||
    !nullable(isUtcInstant)(value.claimedAt) ||
    !isMyr(value.negotiatedDiscount) ||
    !nullable(isMyr)(value.discountLimitAtSubmit) ||
    !isMyr(value.balancePayable) ||
    !nullable(isUtcInstant)(value.submittedAt) ||
    !nullable(isUtcInstant)(value.decidedAt) ||
    !nullable(isUtcInstant)(value.closedAt) ||
    !nullable(text)(value.outcome) ||
    !isUtcInstant(value.updatedAt) ||
    !isPositiveInteger(value.version)
  ) {
    return null;
  }
  return {
    ...value,
    agentSuspended: value.agentSuspended === true,
    customerSuspended: value.customerSuspended === true,
    customerId: customerIdOf(value),
  } as unknown as SaleSummary;
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

function parseDepositRequestRow(value: unknown): DepositRequestRow | null {
  if (
    !isRecord(value) ||
    !isUuid(value.requestId) ||
    !isUuid(value.vehicleId) ||
    !text(value.registration) ||
    !Number.isSafeInteger(value.manufactureYear) ||
    !text(value.make) ||
    !text(value.model) ||
    !text(value.trim) ||
    !isUuid(value.customerId) ||
    !text(value.customer) ||
    !text(value.branch) ||
    !text(value.requestedBy) ||
    !isUtcInstant(value.expiresAtUtc)
  ) {
    return null;
  }
  return value as unknown as DepositRequestRow;
}

export function parseSalesTable(value: unknown): SalesTablePage | null {
  const page = uniquePage(parseCountedPage(value, parseSaleSummary, tableCountKeys));
  const view = isRecord(value)
    ? tableViews.find((item) => item.value === value.view)?.value
    : undefined;
  if (!page?.counts || !view || !isRecord(value)) {
    return null;
  }
  // Only the Awaiting deposit tab carries requests; any other tab must not.
  const requests = value.depositRequests ?? [];
  if (!Array.isArray(requests) || (view !== "AwaitingDeposit" && requests.length > 0)) {
    return null;
  }
  const depositRequests = parseItems(requests, parseDepositRequestRow);
  if (
    !depositRequests ||
    depositRequests.length > page.pageSize ||
    depositRequests.length > page.total ||
    new Set(depositRequests.map((item) => item.requestId)).size !== depositRequests.length
  ) {
    return null;
  }
  return { ...page, counts: page.counts as TableCounts, view, depositRequests };
}
