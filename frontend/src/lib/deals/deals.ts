import { parsePage } from "@/lib/api";
import type { CurrentActor } from "@/lib/auth/actor";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { isMyr } from "@/lib/currency-input";
import { DEFAULT_PAGE_SIZE, isPageSize, paginationQuery } from "@/lib/pagination";
import type { SearchParams } from "@/lib/query";

export const dealViews = [
  { value: "Active", label: "Active" },
  { value: "NeedsApproval", label: "Needs approval" },
  { value: "History", label: "History" },
] as const;

export type DealView = (typeof dealViews)[number]["value"];

export const dealStatuses = ["Draft", "PendingManagerApproval", "Approved", "Cancelled"] as const;
export type DealStatus = (typeof dealStatuses)[number];

export const dealStatusLabels: Record<DealStatus, string> = {
  Draft: "Draft",
  PendingManagerApproval: "Pending approval",
  Approved: "Approved",
  Cancelled: "Cancelled",
};

export const dealSorts = [
  "number",
  "vehicle",
  "customer",
  "agent",
  "status",
  "balance",
  "discount",
  "submitted",
  "updated",
] as const;

export interface DealSummary {
  id: string;
  orderNumber: string | null;
  status: DealStatus;
  nextStep: string;
  vehicleId: string;
  registration: string;
  vehicle: string;
  customerId: string;
  customer: string;
  salesAgentId: string;
  agent: string;
  branchId: string;
  branch: string;
  balancePayable: number;
  negotiatedDiscount: number;
  discountLimitAtSubmit: number | null;
  version: number;
  submittedAtUtc: string | null;
  updatedAtUtc: string;
}

export interface DealCounts {
  active: number;
  needsApproval: number;
  history: number;
}

export interface DealEvent {
  id: string;
  kind: string;
  fromStatus: string | null;
  toStatus: DealStatus;
  actorId: string;
  reason: string | null;
  recordedAtUtc: string;
}

export interface Deal {
  id: string;
  orderNumber: string | null;
  status: DealStatus;
  vehicleId: string;
  customerId: string;
  salesAgentId: string;
  branchId: string;
  reservationId: string | null;
  promoCode: string | null;
  basePrice: number;
  promoDiscount: number;
  negotiatedDiscount: number;
  processingFee: number;
  roadTax: number;
  sstRate: number;
  serviceTax: number;
  warrantyPackageId: string | null;
  warrantyName: string | null;
  warrantyPrice: number;
  depositCredit: number;
  tradeInAppointmentId: string | null;
  tradeInAllowance: number;
  balancePayable: number;
  discountLimitAtSubmit: number | null;
  decidedById: string | null;
  version: number;
  createdAtUtc: string;
  updatedAtUtc: string;
  events: DealEvent[];
}

export interface EligibleVehicle {
  vehicleId: string;
  registration: string;
  label: string;
  price: number;
  hasHold: boolean;
  reservationId: string | null;
  holdCustomer: string | null;
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

export interface DealOptions {
  warranties: WarrantyOption[];
  tradeIns: TradeInOption[];
}

export interface DealFilters {
  view: DealView;
  pageSize: number;
  status: string;
  sort: string;
  direction: "asc" | "desc";
  number: string;
  vehicle: string;
  customer: string;
  search: string;
  agent: string;
  branch: string;
  nextStep: string;
  minimumBalance: string;
  maximumBalance: string;
  minimumDiscount: string;
  maximumDiscount: string;
  submittedFrom: string;
  submittedTo: string;
  updatedFrom: string;
  updatedTo: string;
}

// Every column filter key beyond status, in the order they are sent upstream.
export const dealTextKeys = [
  "number",
  "vehicle",
  "customer",
  "search",
  "agent",
  "branch",
  "nextStep",
  "minimumBalance",
  "maximumBalance",
  "minimumDiscount",
  "maximumDiscount",
  "submittedFrom",
  "submittedTo",
  "updatedFrom",
  "updatedTo",
] as const;

export const dealNextSteps = [
  "Add terms and submit",
  "Edit and resubmit",
  "Awaiting manager approval",
  "Approved",
  "Cancelled",
] as const;

export function hasDealAccess(actor: CurrentActor) {
  return (
    actor.role === "SalesAgent" || actor.role === "BranchManager" || actor.role === "GeneralManager"
  );
}

// Same table for every role; the starting view and ordering follow what each role does first.
export function defaultDealFilters(role: CurrentActor["role"]): DealFilters {
  const manager = role === "BranchManager";
  return {
    view: manager ? "NeedsApproval" : "Active",
    pageSize: DEFAULT_PAGE_SIZE,
    status: "",
    sort: manager ? "submitted" : "updated",
    direction: manager ? "asc" : "desc",
    number: "",
    vehicle: "",
    customer: "",
    search: "",
    agent: "",
    branch: "",
    nextStep: "",
    minimumBalance: "",
    maximumBalance: "",
    minimumDiscount: "",
    maximumDiscount: "",
    submittedFrom: "",
    submittedTo: "",
    updatedFrom: "",
    updatedTo: "",
  };
}

const text = (value: unknown): value is string => typeof value === "string" && value.length > 0;
const utc = (value: unknown): value is string =>
  typeof value === "string" && /(?:Z|\+00:00)$/.test(value) && Number.isFinite(Date.parse(value));
const count = (value: unknown): value is number =>
  Number.isSafeInteger(value) && Number(value) >= 0;
const positive = (value: unknown): value is number => count(value) && Number(value) > 0;
const status = (value: unknown): value is DealStatus => dealStatuses.some((item) => item === value);
const nullableUtc = (value: unknown) => value === null || utc(value);
const nullableText = (value: unknown) => value === null || text(value);
const nullableId = (value: unknown) => value === null || isUuid(value);
const nullableMyr = (value: unknown) => value === null || isMyr(value);

export function parseDealSummary(value: unknown): DealSummary | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !nullableText(value.orderNumber) ||
    !status(value.status) ||
    !text(value.nextStep) ||
    !isUuid(value.vehicleId) ||
    !text(value.registration) ||
    !text(value.vehicle) ||
    !isUuid(value.customerId) ||
    !text(value.customer) ||
    !isUuid(value.salesAgentId) ||
    typeof value.agent !== "string" ||
    !isUuid(value.branchId) ||
    !text(value.branch) ||
    !isMyr(value.balancePayable) ||
    !isMyr(value.negotiatedDiscount) ||
    !nullableMyr(value.discountLimitAtSubmit) ||
    !positive(value.version) ||
    !nullableUtc(value.submittedAtUtc) ||
    !utc(value.updatedAtUtc)
  ) {
    return null;
  }
  return value as unknown as DealSummary;
}

export function parseDealPage(value: unknown) {
  const page = parsePage(value, parseDealSummary);
  const counts = isRecord(value) ? value.counts : null;
  if (
    !page ||
    !isRecord(counts) ||
    !count(counts.active) ||
    !count(counts.needsApproval) ||
    !count(counts.history) ||
    new Set(page.items.map((row) => row.id)).size !== page.items.length
  ) {
    return null;
  }
  return { ...page, counts: counts as unknown as DealCounts };
}

function parseEvent(value: unknown): DealEvent | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !text(value.kind) ||
    !nullableText(value.fromStatus) ||
    !status(value.toStatus) ||
    !isUuid(value.actorId) ||
    !nullableText(value.reason) ||
    !utc(value.recordedAtUtc)
  ) {
    return null;
  }
  return value as unknown as DealEvent;
}

export function parseDeal(value: unknown): Deal | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !nullableText(value.orderNumber) ||
    !status(value.status) ||
    !isUuid(value.vehicleId) ||
    !isUuid(value.customerId) ||
    !isUuid(value.salesAgentId) ||
    !isUuid(value.branchId) ||
    !nullableId(value.reservationId) ||
    !nullableText(value.promoCode) ||
    ![
      value.basePrice,
      value.promoDiscount,
      value.negotiatedDiscount,
      value.processingFee,
      value.roadTax,
      value.serviceTax,
      value.warrantyPrice,
      value.depositCredit,
      value.tradeInAllowance,
      value.balancePayable,
    ].every(isMyr) ||
    typeof value.sstRate !== "number" ||
    !Number.isFinite(value.sstRate) ||
    !nullableId(value.warrantyPackageId) ||
    !nullableText(value.warrantyName) ||
    !nullableId(value.tradeInAppointmentId) ||
    !nullableMyr(value.discountLimitAtSubmit) ||
    !nullableId(value.decidedById) ||
    !positive(value.version) ||
    !utc(value.createdAtUtc) ||
    !utc(value.updatedAtUtc) ||
    !Array.isArray(value.events)
  ) {
    return null;
  }
  const events = value.events.map(parseEvent);
  return events.every((event) => event !== null) ? ({ ...value, events } as unknown as Deal) : null;
}

export function parseEligibleVehicles(value: unknown): EligibleVehicle[] | null {
  if (!Array.isArray(value)) {
    return null;
  }
  const vehicles = value.map((item) =>
    isRecord(item) &&
    isUuid(item.vehicleId) &&
    text(item.registration) &&
    text(item.label) &&
    isMyr(item.price) &&
    typeof item.hasHold === "boolean" &&
    nullableId(item.reservationId) &&
    nullableText(item.holdCustomer)
      ? (item as unknown as EligibleVehicle)
      : null,
  );
  return vehicles.every((item) => item !== null) ? (vehicles as EligibleVehicle[]) : null;
}

export function parseDealOptions(value: unknown): DealOptions | null {
  if (!isRecord(value) || !Array.isArray(value.warranties) || !Array.isArray(value.tradeIns)) {
    return null;
  }
  const warranties = value.warranties.every(
    (item) =>
      isRecord(item) &&
      isUuid(item.id) &&
      text(item.name) &&
      positive(item.durationMonths) &&
      positive(item.mileageCapKm) &&
      isMyr(item.price) &&
      text(item.minimumGrade),
  );
  const tradeIns = value.tradeIns.every(
    (item) =>
      isRecord(item) &&
      isUuid(item.appointmentId) &&
      text(item.label) &&
      isMyr(item.minimum) &&
      isMyr(item.maximum),
  );
  return warranties && tradeIns ? (value as unknown as DealOptions) : null;
}

export function dealParams(params: SearchParams, role: CurrentActor["role"]) {
  const defaults = defaultDealFilters(role);
  const page = Number(params.page);
  const size = Number(params.pageSize);
  const sorted = dealSorts.find((sort) => sort === params.sort);
  const filters: DealFilters = {
    ...defaults,
    view: dealViews.find((view) => view.value === params.view)?.value ?? defaults.view,
    pageSize: isPageSize(size) ? size : defaults.pageSize,
    status: dealStatuses.find((item) => item === params.status) ?? "",
    sort: sorted ?? defaults.sort,
    direction:
      params.direction === "asc" || params.direction === "desc"
        ? params.direction
        : defaults.direction,
  };
  for (const key of dealTextKeys) {
    const value = params[key];
    if (typeof value === "string") {
      filters[key] = value;
    }
  }
  if (!dealNextSteps.some((step) => step === filters.nextStep)) {
    filters.nextStep = "";
  }
  return { filters, page: Number.isSafeInteger(page) && page > 0 ? page : 1 };
}

export function dealQuery(filters: DealFilters, page = 1) {
  const query = paginationQuery(page, filters.pageSize);
  query.set("view", filters.view);
  if (filters.sort) {
    query.set("sort", filters.sort);
    query.set("direction", filters.direction);
  }
  for (const key of ["status", ...dealTextKeys] as const) {
    const value = filters[key].trim();
    if (value) {
      query.set(key, value);
    }
  }
  return query.toString();
}

export const dealPath = (id: string) => `/api/staff/deals/${id}`;
