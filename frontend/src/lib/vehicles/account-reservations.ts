import { parsePage, type Page } from "@/lib/api";
import { parseActor, type CurrentActor } from "@/lib/auth/actor";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { timestamp } from "@/lib/inspection/parse";
import type { SearchParams } from "@/lib/query";
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
    !timestamp(value.recordedAt) ||
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
    (value.isActive && value.release !== null) ||
    (!value.isActive && (!release || release.reservationId !== receipt.id)) ||
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
  };
}

export function parseAccountReservations(value: unknown): AccountReservations | null {
  if (!isRecord(value)) {
    return null;
  }
  const page = parsePage(value, parseAccountReservation);
  const actor = parseActor(value.actor);
  const active = value.active === null ? null : parseAccountReservation(value.active);
  if (
    !page ||
    page.pageSize !== 25 ||
    !actor ||
    actor.role !== "Customer" ||
    (value.active !== null && !active?.isActive) ||
    new Set(page.items.map((item) => item.receipt.id)).size !== page.items.length ||
    page.items.some((item) => item.isActive && item.receipt.id !== active?.receipt.id)
  ) {
    return null;
  }
  const expectedCount = Math.max(0, Math.min(25, page.total - (page.page - 1) * 25));
  if (page.items.length !== expectedCount) {
    return null;
  }
  return { ...page, actor, active };
}

export function accountReservationPage(params: SearchParams) {
  const page = Number(params.reservationPage);
  return Number.isSafeInteger(page) && page > 0 && page <= 2147483647 ? page : 1;
}

export function reservationVehicleName(item: AccountReservation) {
  return `${item.manufactureYear} ${item.make} ${item.model} ${item.trim}`;
}
