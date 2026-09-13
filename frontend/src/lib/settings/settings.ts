import { parsePage } from "@/lib/api";
import type { CurrentActor } from "@/lib/auth/actor";
import { isPositiveInteger, isRecord, isUtcInstant, isUuid } from "@/lib/auth/contracts";
import { isMyr } from "@/lib/currency-input";
import { DEFAULT_PAGE_SIZE, isPageSize, paginationQuery } from "@/lib/pagination";
import type { SearchParams } from "@/lib/query";

export const settingsSections = [
  { value: "settings", label: "Settings" },
  { value: "promos", label: "Promos" },
  { value: "warranties", label: "Warranty packages" },
  { value: "retention", label: "Retention" },
] as const;

export type SettingsSection = (typeof settingsSections)[number]["value"];

// Settings and Retention each show one record; Promos and Warranties show lists with a record drawer.
export type RecordSection = "settings" | "retention";
export type ListSection = Exclude<SettingsSection, RecordSection>;

export function isRecordSection(section: SettingsSection): section is RecordSection {
  return section === "settings" || section === "retention";
}

export const warrantyGrades = ["A", "B", "C"] as const;
export type WarrantyGrade = (typeof warrantyGrades)[number];

export const promoSorts = ["code", "discount", "expiresAt", "status"] as const;
export const warrantySorts = ["name", "term", "mileage", "price", "grade", "status"] as const;
export const promoStatuses = ["all", "active", "inactive", "expired"] as const;
export const warrantyStatuses = ["all", "active", "inactive"] as const;

export interface SettingsBounds {
  maximumFee: number;
  maximumSstRate: number;
  maximumDiscount: number;
  minimumHoldHours: number;
  maximumHoldHours: number;
}

export interface PurchaseSettings {
  processingFee: number;
  sstRate: number;
  maxAgentDiscountAmount: number;
  reservationHoldHours: number;
  updatedAtUtc: string;
  version: number;
  bounds: SettingsBounds;
}

export interface Promo {
  id: string;
  code: string;
  discount: number;
  expiresAtUtc: string;
  isActive: boolean;
  version: number;
}

export function promoState(promo: Promo, now = Date.now()) {
  if (Date.parse(promo.expiresAtUtc) <= now) {
    return "Expired";
  }
  return promo.isActive ? "Active" : "Inactive";
}

export interface Warranty {
  id: string;
  name: string;
  durationMonths: number;
  mileageCapKm: number;
  price: number;
  minimumGrade: WarrantyGrade;
  isActive: boolean;
  version: number;
}

export interface ListFilters {
  pageSize: number;
  search: string;
  status: string;
  sort: string;
  direction: "asc" | "desc";
  // Column filters: text on the identifying column, an amount range, Malaysia dates and a grade.
  text: string;
  minimum: string;
  maximum: string;
  expiresFrom: string;
  expiresTo: string;
  grade: string;
  termFrom: string;
  termTo: string;
  mileageFrom: string;
  mileageTo: string;
}

export const columnFilterKeys = [
  "text",
  "minimum",
  "maximum",
  "expiresFrom",
  "expiresTo",
  "grade",
  "termFrom",
  "termTo",
  "mileageFrom",
  "mileageTo",
] as const;

export function hasSettingsAccess(actor: CurrentActor) {
  return actor.role === "Administrator";
}

const finite = (value: unknown): value is number =>
  typeof value === "number" && Number.isFinite(value);

function parseBounds(value: unknown): SettingsBounds | null {
  if (
    !isRecord(value) ||
    !isMyr(value.maximumFee) ||
    !finite(value.maximumSstRate) ||
    !isMyr(value.maximumDiscount) ||
    !isPositiveInteger(value.minimumHoldHours) ||
    !isPositiveInteger(value.maximumHoldHours) ||
    value.maximumHoldHours < value.minimumHoldHours
  ) {
    return null;
  }
  return {
    maximumFee: value.maximumFee,
    maximumSstRate: value.maximumSstRate,
    maximumDiscount: value.maximumDiscount,
    minimumHoldHours: value.minimumHoldHours,
    maximumHoldHours: value.maximumHoldHours,
  };
}

export function parseSettings(value: unknown): PurchaseSettings | null {
  const bounds = isRecord(value) ? parseBounds(value.bounds) : null;
  if (
    !isRecord(value) ||
    !bounds ||
    !isMyr(value.processingFee) ||
    !finite(value.sstRate) ||
    value.sstRate < 0 ||
    value.sstRate > bounds.maximumSstRate ||
    !isMyr(value.maxAgentDiscountAmount) ||
    !isPositiveInteger(value.reservationHoldHours) ||
    !isUtcInstant(value.updatedAtUtc) ||
    !isPositiveInteger(value.version)
  ) {
    return null;
  }
  return {
    processingFee: value.processingFee,
    sstRate: value.sstRate,
    maxAgentDiscountAmount: value.maxAgentDiscountAmount,
    reservationHoldHours: value.reservationHoldHours,
    updatedAtUtc: value.updatedAtUtc,
    version: value.version,
    bounds,
  };
}

export function parsePromo(value: unknown): Promo | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    typeof value.code !== "string" ||
    value.code.length === 0 ||
    !isMyr(value.discount) ||
    value.discount <= 0 ||
    !isUtcInstant(value.expiresAtUtc) ||
    typeof value.isActive !== "boolean" ||
    !isPositiveInteger(value.version)
  ) {
    return null;
  }
  return {
    id: value.id,
    code: value.code,
    discount: value.discount,
    expiresAtUtc: value.expiresAtUtc,
    isActive: value.isActive,
    version: value.version,
  };
}

export function parseWarranty(value: unknown): Warranty | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    typeof value.name !== "string" ||
    value.name.length === 0 ||
    !isPositiveInteger(value.durationMonths) ||
    !isPositiveInteger(value.mileageCapKm) ||
    !isMyr(value.price) ||
    !warrantyGrades.some((grade) => grade === value.minimumGrade) ||
    typeof value.isActive !== "boolean" ||
    !isPositiveInteger(value.version)
  ) {
    return null;
  }
  return {
    id: value.id,
    name: value.name,
    durationMonths: value.durationMonths,
    mileageCapKm: value.mileageCapKm,
    price: value.price,
    minimumGrade: value.minimumGrade as WarrantyGrade,
    isActive: value.isActive,
    version: value.version,
  };
}

function uniquePage<T extends { id: string }>(
  value: unknown,
  parseItem: (item: unknown) => T | null,
) {
  const page = parsePage(value, parseItem);
  if (!page || new Set(page.items.map((row) => row.id)).size !== page.items.length) {
    return null;
  }
  return page;
}

export const parsePromos = (value: unknown) => uniquePage(value, parsePromo);
export const parseWarranties = (value: unknown) => uniquePage(value, parseWarranty);

export function listParams(params: SearchParams, section: SettingsSection) {
  const sorts: readonly string[] = section === "promos" ? promoSorts : warrantySorts;
  const statuses: readonly string[] = section === "promos" ? promoStatuses : warrantyStatuses;
  const page = Number(params.page);
  const size = Number(params.pageSize);
  const filters: ListFilters = {
    pageSize: isPageSize(size) ? size : DEFAULT_PAGE_SIZE,
    search: typeof params.search === "string" ? params.search : "",
    status: statuses.find((status) => status === params.view) ?? "all",
    sort: sorts.find((sort) => sort === params.sort) ?? "",
    direction: params.direction === "desc" ? "desc" : "asc",
    ...emptyColumnFilters,
  };
  for (const key of columnFilterKeys) {
    const value = params[key];
    if (typeof value === "string") {
      filters[key] = value;
    }
  }
  if (!warrantyGrades.some((grade) => grade === filters.grade)) {
    filters.grade = "";
  }
  return { filters, page: Number.isSafeInteger(page) && page > 0 ? page : 1 };
}

const emptyColumnFilters = {
  text: "",
  minimum: "",
  maximum: "",
  expiresFrom: "",
  expiresTo: "",
  grade: "",
  termFrom: "",
  termTo: "",
  mileageFrom: "",
  mileageTo: "",
};

export const emptyListFilters: ListFilters = {
  pageSize: DEFAULT_PAGE_SIZE,
  search: "",
  status: "all",
  sort: "",
  direction: "asc",
  ...emptyColumnFilters,
};

export function listQuery(filters: ListFilters, page = 1) {
  const query = paginationQuery(page, filters.pageSize);
  const search = filters.search.trim();
  if (search) {
    query.set("search", search);
  }
  if (filters.status !== "all") {
    query.set("view", filters.status);
  }
  if (filters.sort) {
    query.set("sort", filters.sort);
    query.set("direction", filters.direction);
  }
  for (const key of columnFilterKeys) {
    const value = filters[key].trim();
    if (value) {
      query.set(key, value);
    }
  }
  return query.toString();
}

// The browser address omits every default: first page, default page size, all statuses and no explicit sort.
export function listUrlQuery(filters: ListFilters, page = 1) {
  const query = new URLSearchParams(listQuery(filters, page));
  if (page === 1) {
    query.delete("page");
  }
  if (filters.pageSize === DEFAULT_PAGE_SIZE) {
    query.delete("pageSize");
  }
  return query;
}

export function recordPath(section: ListSection, id: string) {
  return `/api/staff/settings/${section}/${id}`;
}

export function sectionPath(section: SettingsSection, filters: ListFilters, page = 1) {
  if (section === "settings") {
    return "/api/staff/settings";
  }
  if (section === "retention") {
    return "/api/staff/settings/retention";
  }
  return `/api/staff/settings/${section}?${listQuery(filters, page)}`;
}
