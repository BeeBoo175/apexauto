import { isRecord, isUuid } from "@/lib/auth/contracts";
import { isMyr } from "@/lib/currency-input";

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

export const salePath = (id: string) => `/api/staff/sales/${id}`;
