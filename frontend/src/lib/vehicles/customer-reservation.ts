import { isRecord, isUtcInstant, isUuid } from "@/lib/auth/contracts";
import { parsePurchasePrice, type PurchasePrice } from "./purchase-quote";
import { providerPaymentAddress } from "./payment-address";
import { RESERVATION_DEPOSIT, RESERVATION_HOLD_HOURS } from "./deposit-terms";

export interface CustomerReservationRequest {
  operationId: string;
  publicationId: string;
  promoId: string | null;
}

// The reference customers and staff quote, VRS-YYYY-NNNNNN.
export const reservationNumber = (value: unknown): value is string =>
  typeof value === "string" && /^VRS-[0-9]{4}-[0-9]{6}$/.test(value);

export interface CustomerReservationReceipt {
  id: string;
  number: string;
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

// With the payment gateway on, reserving answers with the payment page to open instead of a receipt.
export interface CustomerReservationPayment {
  paymentUrl: string;
}

export type CustomerReservationOutcome = CustomerReservationReceipt | CustomerReservationPayment;

export function isReservationPayment(
  value: CustomerReservationOutcome,
): value is CustomerReservationPayment {
  return "paymentUrl" in value;
}

export function parseCustomerReservationOutcome(value: unknown): CustomerReservationOutcome | null {
  const paymentUrl = providerPaymentAddress(value);
  return paymentUrl ? { paymentUrl } : parseCustomerReservation(value);
}

export function parseCustomerReservation(value: unknown): CustomerReservationReceipt | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !reservationNumber(value.number) ||
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
    value.deposit !== RESERVATION_DEPOSIT ||
    !isUtcInstant(value.createdAt) ||
    !isUtcInstant(value.expiresAt) ||
    Date.parse(value.expiresAt) - Date.parse(value.createdAt) !==
      RESERVATION_HOLD_HOURS * 60 * 60 * 1000
  ) {
    return null;
  }
  const price = parsePurchasePrice(value.price);
  if (!price || (value.promoId === null && price.promoDiscount !== 0)) {
    return null;
  }
  return {
    id: value.id,
    number: value.number,
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
  // Null when staff reserve for a customer and only masked contact is known.
  email: string | null,
) {
  const request = parseCustomerReservationRequest(body);
  return Boolean(
    request &&
    receipt.id === request.operationId &&
    receipt.vehicleId === vehicleId &&
    receipt.branchId === branchId &&
    receipt.publicationId === request.publicationId &&
    receipt.promoId === request.promoId &&
    (email === null || receipt.buyer.email === email),
  );
}
