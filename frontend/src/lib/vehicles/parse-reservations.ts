import { parseCountedPage } from "@/lib/api";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { isMyr } from "@/lib/currency-input";
import { reservationNumber } from "./customer-reservation";
import {
  reservationCountKeys,
  reservationStatuses,
  type ReservationClaim,
  type ReservationItem,
} from "./reservations";

const text = (value: unknown): value is string =>
  typeof value === "string" && value.trim().length > 0;
const timestamp = (value: unknown): value is string =>
  text(value) && /(?:Z|\+00:00)$/.test(value) && Number.isFinite(Date.parse(value));

// A claim is absent on open and released holds; undefined means the claim is malformed.
function parseClaim(value: unknown): ReservationClaim | null | undefined {
  if (value === undefined || value === null) {
    return null;
  }
  if (
    !isRecord(value) ||
    !text(value.agent) ||
    !timestamp(value.claimedAtUtc) ||
    !Number.isSafeInteger(value.frozenRemainingSeconds) ||
    Number(value.frozenRemainingSeconds) < 0
  ) {
    return undefined;
  }
  return {
    agent: value.agent,
    claimedAtUtc: value.claimedAtUtc,
    frozenRemainingSeconds: Number(value.frozenRemainingSeconds),
  };
}

export function parseReservation(value: unknown): ReservationItem | null {
  if (!isRecord(value) || !isRecord(value.receipt)) {
    return null;
  }
  const receipt = value.receipt;
  const buyer = receipt.buyer;
  const price = receipt.price;
  if (
    !isUuid(receipt.id) ||
    !reservationNumber(receipt.number) ||
    !isUuid(receipt.vehicleId) ||
    !isUuid(receipt.branchId) ||
    !isRecord(buyer) ||
    !text(buyer.legalName) ||
    !isRecord(price) ||
    !isMyr(price.total) ||
    !timestamp(receipt.createdAt) ||
    !timestamp(receipt.expiresAt) ||
    Date.parse(receipt.expiresAt) <= Date.parse(receipt.createdAt) ||
    !text(value.branchName) ||
    !text(value.registrationPlate) ||
    !text(value.make) ||
    !text(value.model) ||
    !text(value.trim) ||
    !Number.isSafeInteger(value.manufactureYear) ||
    Number(value.manufactureYear) < 1 ||
    typeof value.isActive !== "boolean"
  ) {
    return null;
  }
  const claim = parseClaim(value.claim);
  if (claim === undefined || (claim !== null && !value.isActive)) {
    return null;
  }
  const release = value.release;
  let status: ReservationItem["status"] = "Active";
  if (!value.isActive) {
    if (
      !isRecord(release) ||
      !isUuid(release.id) ||
      release.reservationId !== receipt.id ||
      !timestamp(release.recordedAt) ||
      Date.parse(release.recordedAt) < Date.parse(receipt.createdAt) ||
      typeof release.kind !== "string" ||
      release.kind === "Active" ||
      !Object.hasOwn(reservationStatuses, release.kind)
    ) {
      return null;
    }
    status = release.kind as ReservationItem["status"];
  } else if (release !== null) {
    return null;
  }
  return {
    id: receipt.id,
    number: receipt.number,
    vehicleId: receipt.vehicleId,
    branchId: receipt.branchId,
    branchName: value.branchName,
    vehicle: `${value.manufactureYear} ${value.make} ${value.model} ${value.trim}`,
    registrationPlate: value.registrationPlate,
    make: value.make,
    model: value.model,
    trim: value.trim,
    manufactureYear: Number(value.manufactureYear),
    releasedAt: isRecord(release) ? String(release.recordedAt) : null,
    buyer: buyer.legalName,
    createdAt: receipt.createdAt,
    expiresAt: receipt.expiresAt,
    total: price.total,
    status,
    claim,
  };
}

export function parseReservations(value: unknown) {
  const page = parseCountedPage(value, parseReservation, reservationCountKeys);
  if (!page || new Set(page.items.map((row) => row.id)).size !== page.items.length) {
    return null;
  }
  return page;
}

export function parseReservationRow(value: unknown): ReservationItem | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !reservationNumber(value.number) ||
    !isUuid(value.vehicleId) ||
    !isUuid(value.branchId) ||
    !text(value.branchName) ||
    !text(value.vehicle) ||
    !text(value.registrationPlate) ||
    !text(value.make) ||
    !text(value.model) ||
    !text(value.trim) ||
    !Number.isSafeInteger(value.manufactureYear) ||
    Number(value.manufactureYear) < 1 ||
    !text(value.buyer) ||
    !timestamp(value.createdAt) ||
    !timestamp(value.expiresAt) ||
    Date.parse(value.expiresAt) <= Date.parse(value.createdAt) ||
    !isMyr(value.total) ||
    typeof value.status !== "string" ||
    !Object.hasOwn(reservationStatuses, value.status)
  ) {
    return null;
  }
  if (
    (value.status === "Active" && value.releasedAt !== null) ||
    (value.status !== "Active" &&
      (!timestamp(value.releasedAt) || Date.parse(value.releasedAt) < Date.parse(value.createdAt)))
  ) {
    return null;
  }
  const claim = parseClaim(value.claim);
  if (claim === undefined || (claim !== null && value.status !== "Active")) {
    return null;
  }
  return {
    id: value.id,
    number: value.number,
    vehicleId: value.vehicleId,
    branchId: value.branchId,
    branchName: value.branchName,
    vehicle: `${value.manufactureYear} ${value.make} ${value.model} ${value.trim}`,
    registrationPlate: value.registrationPlate,
    make: value.make,
    model: value.model,
    trim: value.trim,
    manufactureYear: Number(value.manufactureYear),
    releasedAt: value.releasedAt as string | null,
    buyer: value.buyer,
    createdAt: value.createdAt,
    expiresAt: value.expiresAt,
    total: value.total,
    status: value.status as ReservationItem["status"],
    claim,
  };
}

export function parseReservationSnapshot(value: unknown) {
  const page = parseCountedPage(value, parseReservationRow, reservationCountKeys);
  if (!page || new Set(page.items.map((row) => row.id)).size !== page.items.length) {
    return null;
  }
  return page;
}
