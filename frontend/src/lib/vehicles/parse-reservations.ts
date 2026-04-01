import { parsePage } from "@/lib/api";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { reservationStatuses, type ReservationItem } from "./reservations";

const text = (value: unknown): value is string =>
  typeof value === "string" && value.trim().length > 0;
const timestamp = (value: unknown): value is string =>
  text(value) && /(?:Z|\+00:00)$/.test(value) && Number.isFinite(Date.parse(value));

function parseReservation(value: unknown): ReservationItem | null {
  if (!isRecord(value) || !isRecord(value.receipt)) {
    return null;
  }
  const receipt = value.receipt;
  const buyer = receipt.buyer;
  const price = receipt.price;
  if (
    !isUuid(receipt.id) ||
    !isUuid(receipt.vehicleId) ||
    !isUuid(receipt.branchId) ||
    !isRecord(buyer) ||
    !text(buyer.legalName) ||
    !isRecord(price) ||
    typeof price.total !== "number" ||
    !Number.isFinite(price.total) ||
    price.total < 0 ||
    !Number.isSafeInteger(Math.round(price.total * 100)) ||
    !timestamp(receipt.createdAt) ||
    !timestamp(receipt.expiresAt) ||
    Date.parse(receipt.expiresAt) <= Date.parse(receipt.createdAt) ||
    !text(value.branchName) ||
    !text(value.make) ||
    !text(value.model) ||
    !text(value.trim) ||
    !Number.isSafeInteger(value.manufactureYear) ||
    Number(value.manufactureYear) < 1 ||
    typeof value.isActive !== "boolean"
  ) {
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
    vehicleId: receipt.vehicleId,
    branchId: receipt.branchId,
    branchName: value.branchName,
    vehicle: `${value.manufactureYear} ${value.make} ${value.model} ${value.trim}`,
    buyer: buyer.legalName,
    createdAt: receipt.createdAt,
    expiresAt: receipt.expiresAt,
    total: price.total,
    status,
  };
}

export function parseReservations(value: unknown) {
  const page = parsePage(value, parseReservation);
  if (!page || new Set(page.items.map((row) => row.id)).size !== page.items.length) {
    return null;
  }
  return page;
}

function parseReservationRow(value: unknown): ReservationItem | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !isUuid(value.vehicleId) ||
    !isUuid(value.branchId) ||
    !text(value.branchName) ||
    !text(value.vehicle) ||
    !text(value.buyer) ||
    !timestamp(value.createdAt) ||
    !timestamp(value.expiresAt) ||
    Date.parse(value.expiresAt) <= Date.parse(value.createdAt) ||
    typeof value.total !== "number" ||
    !Number.isFinite(value.total) ||
    value.total < 0 ||
    !Number.isSafeInteger(Math.round(value.total * 100)) ||
    typeof value.status !== "string" ||
    !Object.hasOwn(reservationStatuses, value.status)
  ) {
    return null;
  }
  return {
    id: value.id,
    vehicleId: value.vehicleId,
    branchId: value.branchId,
    branchName: value.branchName,
    vehicle: value.vehicle,
    buyer: value.buyer,
    createdAt: value.createdAt,
    expiresAt: value.expiresAt,
    total: value.total,
    status: value.status as ReservationItem["status"],
  };
}

export function parseReservationSnapshot(value: unknown) {
  const page = parsePage(value, parseReservationRow);
  if (!page || new Set(page.items.map((row) => row.id)).size !== page.items.length) {
    return null;
  }
  return page;
}
