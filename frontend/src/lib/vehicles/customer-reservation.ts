import { isRecord, isUuid, type CustomerAccount } from "@/lib/auth/contracts";
import type { CurrentActor } from "@/lib/auth/actor";
import { timestamp } from "@/lib/inspection/parse";
import { parsePurchasePrice, type PurchasePrice } from "./purchase-quote";

export interface ReservationAccess {
  status: number;
  actor: CurrentActor | null;
  account: CustomerAccount | null;
}

export interface CustomerReservationRequest {
  operationId: string;
  publicationId: string;
  promoId: string | null;
}

export interface CustomerReservationReceipt {
  id: string;
  vehicleId: string;
  branchId: string;
  publicationId: string;
  buyer: { legalName: string; email: string };
  promoId: string | null;
  promoCode: string | null;
  price: PurchasePrice;
  deposit: number;
  createdAt: string;
  expiresAt: string;
}

export function parseCustomerReservationRequest(value: unknown): CustomerReservationRequest | null {
  if (
    !isRecord(value) ||
    !isUuid(value.operationId) ||
    !isUuid(value.publicationId) ||
    (value.promoId !== null && !isUuid(value.promoId))
  ) {
    return null;
  }
  return {
    operationId: value.operationId,
    publicationId: value.publicationId,
    promoId: value.promoId,
  };
}

export function parseCustomerReservation(value: unknown): CustomerReservationReceipt | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !isUuid(value.vehicleId) ||
    !isUuid(value.branchId) ||
    !isUuid(value.publicationId) ||
    !isRecord(value.buyer) ||
    typeof value.buyer.legalName !== "string" ||
    !value.buyer.legalName.trim() ||
    typeof value.buyer.email !== "string" ||
    !value.buyer.email.trim() ||
    (value.promoId !== null && !isUuid(value.promoId)) ||
    (value.promoId === null && value.promoCode !== null) ||
    (value.promoId !== null && (typeof value.promoCode !== "string" || !value.promoCode.trim())) ||
    value.deposit !== 500 ||
    !timestamp(value.createdAt) ||
    !timestamp(value.expiresAt) ||
    Date.parse(value.expiresAt) - Date.parse(value.createdAt) !== 48 * 60 * 60 * 1000
  ) {
    return null;
  }
  const price = parsePurchasePrice(value.price);
  if (!price || (value.promoId === null && price.promoDiscount !== 0)) {
    return null;
  }
  return {
    id: value.id,
    vehicleId: value.vehicleId,
    branchId: value.branchId,
    publicationId: value.publicationId,
    buyer: { legalName: value.buyer.legalName, email: value.buyer.email },
    promoId: value.promoId,
    promoCode: typeof value.promoCode === "string" ? value.promoCode : null,
    price,
    deposit: value.deposit,
    createdAt: value.createdAt,
    expiresAt: value.expiresAt,
  };
}

export function matchesCustomerReservation(
  receipt: CustomerReservationReceipt,
  body: unknown,
  vehicleId: string,
  branchId: string,
  email: string,
) {
  const request = parseCustomerReservationRequest(body);
  return Boolean(
    request &&
    receipt.id === request.operationId &&
    receipt.vehicleId === vehicleId &&
    receipt.branchId === branchId &&
    receipt.publicationId === request.publicationId &&
    receipt.promoId === request.promoId &&
    receipt.buyer.email === email,
  );
}
