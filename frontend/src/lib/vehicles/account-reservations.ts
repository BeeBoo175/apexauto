import { parsePage, type Page } from "@/lib/api";
import { parseActor, type CurrentActor } from "@/lib/auth/actor";
import { isRecord, isUtcInstant, isUuid } from "@/lib/auth/contracts";
import { ACCOUNT_PAGE_SIZE } from "@/lib/pagination";
import type { SearchParams } from "@/lib/query";
import type { HistoryFilters } from "@/lib/intake/list-query";
import { parseCustomerReservation, type CustomerReservationReceipt } from "./customer-reservation";

export const customerReservationStates = {
  CustomerCancelled: "Cancelled by you",
  StaffCancelled: "Cancelled by ApexAuto",
  Expired: "Expired",
  Withdrawn: "Listing withdrawn",
  Reinspection: "Returned for inspection",
} as const;

export interface CustomerReservationRelease {
  id: string;
  reservationId: string;
  kind: keyof typeof customerReservationStates;
  recordedAt: string;
  reason: string | null;
  refund: number;
}

export interface AccountReservation {
  receipt: CustomerReservationReceipt;
  branchName: string;
  make: string;
  model: string;
  trim: string;
  manufactureYear: number;
  coverPhotoId: string | null;
  isActive: boolean;
  release: CustomerReservationRelease | null;
  // Our sales team is handling the hold, so it shows no countdown and is not cancelled online.
  isHandled: boolean;
  // The running deadline, which a released claim resumes; empty while the sale is handled.
  dueAt: string | null;
}

export interface AccountReservations extends Page<AccountReservation> {
  actor: CurrentActor;
  active: AccountReservation | null;
}

function parseRelease(value: unknown): CustomerReservationRelease | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !isUuid(value.reservationId) ||
    typeof value.kind !== "string" ||
    !Object.hasOwn(customerReservationStates, value.kind) ||
    !isUtcInstant(value.recordedAt) ||
    value.refund !== 500 ||
    (value.kind === "CustomerCancelled" && value.reason !== null) ||
    (value.kind === "StaffCancelled" && !value.reason) ||
    (value.reason !== null && (typeof value.reason !== "string" || !value.reason.trim()))
  ) {
    return null;
  }
  return {
    id: value.id,
    reservationId: value.reservationId,
    kind: value.kind as CustomerReservationRelease["kind"],
    recordedAt: value.recordedAt,
    reason: value.reason,
    refund: value.refund,
  };
}

export function parseAccountReservation(value: unknown): AccountReservation | null {
  if (!isRecord(value)) {
    return null;
  }
  const receipt = parseCustomerReservation(value.receipt);
  const release = value.release === null ? null : parseRelease(value.release);
  const texts = [value.branchName, value.make, value.model, value.trim];
  if (
    !receipt ||
    !texts.every((text) => typeof text === "string" && text.trim().length > 0) ||
    !Number.isSafeInteger(value.manufactureYear) ||
    Number(value.manufactureYear) < 1 ||
    (value.coverPhotoId !== null && !isUuid(value.coverPhotoId)) ||
    typeof value.isActive !== "boolean" ||
    typeof value.isHandled !== "boolean" ||
    (value.isHandled && !value.isActive) ||
    (value.dueAt !== null && !isUtcInstant(value.dueAt)) ||
    (value.isActive && value.release !== null) ||
    (!value.isActive &&
      value.release !== null &&
      (!release || release.reservationId !== receipt.id)) ||
    (release && Date.parse(release.recordedAt) < Date.parse(receipt.createdAt))
  ) {
    return null;
  }
  return {
    receipt,
    branchName: String(value.branchName),
    make: String(value.make),
    model: String(value.model),
    trim: String(value.trim),
    manufactureYear: Number(value.manufactureYear),
    coverPhotoId: value.coverPhotoId,
    isActive: value.isActive,
    release,
    isHandled: value.isHandled,
    dueAt: value.dueAt,
  };
}

export function parseAccountReservations(value: unknown): AccountReservations | null {
  if (!isRecord(value)) {
    return null;
  }
  const page = parsePage(value, parseAccountReservation, ACCOUNT_PAGE_SIZE);
  const actor = parseActor(value.actor);
  const active = value.active === null ? null : parseAccountReservation(value.active);
  if (
    !page ||
    page.pageSize !== ACCOUNT_PAGE_SIZE ||
    !actor ||
    actor.role !== "Customer" ||
    (value.active !== null && !active?.isActive) ||
    new Set(page.items.map((item) => item.receipt.id)).size !== page.items.length ||
    page.items.some((item) => item.isActive && item.receipt.id !== active?.receipt.id)
  ) {
    return null;
  }
  const expectedCount = Math.max(
    0,
    Math.min(ACCOUNT_PAGE_SIZE, page.total - (page.page - 1) * ACCOUNT_PAGE_SIZE),
  );
  if (page.items.length !== expectedCount) {
    return null;
  }
  return { ...page, actor, active };
}

export interface ReservationFilters extends HistoryFilters {
  search: string;
}

// The section path scopes these keys, so they use the same names as every other history list.
export function accountReservationFilters(params: SearchParams): ReservationFilters {
  return {
    from: typeof params.from === "string" ? params.from : "",
    to: typeof params.to === "string" ? params.to : "",
    search: typeof params.search === "string" ? params.search.slice(0, 160) : "",
  };
}

export function accountReservationQuery(page: number, filters: ReservationFilters) {
  const query = new URLSearchParams({ page: String(page) });
  if (filters.from) {
    query.set("from", filters.from);
  }
  if (filters.to) {
    query.set("to", filters.to);
  }
  if (filters.search.trim()) {
    query.set("search", filters.search.trim());
  }
  return query.toString();
}

export function accountReservationPage(params: SearchParams) {
  const page = Number(params.page);
  return Number.isSafeInteger(page) && page > 0 && page <= 2147483647 ? page : 1;
}

export function reservationVehicleName(item: AccountReservation) {
  return `${item.manufactureYear} ${item.make} ${item.model} ${item.trim}`;
}
