import { isRecord, isUtcInstant, isUuid } from "@/lib/auth/contracts";
import { parseCustomerReservationRequest } from "./customer-reservation";

// Staff asking a customer to confirm the simulated deposit hold the car for 24 hours.
export const DEPOSIT_REQUEST_HOURS = 24;

export interface DepositRequestReceipt {
  id: string;
  vehicleId: string;
  publicationId: string;
  promoId: string | null;
  createdAt: string;
  expiresAt: string;
}

export function parseDepositRequestReceipt(value: unknown): DepositRequestReceipt | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !isUuid(value.vehicleId) ||
    !isUuid(value.publicationId) ||
    (value.promoId !== null && !isUuid(value.promoId)) ||
    !isUtcInstant(value.createdAt) ||
    !isUtcInstant(value.expiresAt) ||
    Date.parse(value.expiresAt) - Date.parse(value.createdAt) !==
      DEPOSIT_REQUEST_HOURS * 60 * 60 * 1000
  ) {
    return null;
  }
  return {
    id: value.id,
    vehicleId: value.vehicleId,
    publicationId: value.publicationId,
    promoId: value.promoId,
    createdAt: value.createdAt,
    expiresAt: value.expiresAt,
  };
}

// The receipt must answer this exact captured request, not another one for the same car.
export function matchesDepositRequest(
  receipt: DepositRequestReceipt,
  body: unknown,
  vehicleId: string,
) {
  const request = parseCustomerReservationRequest(body);
  return Boolean(
    request &&
    receipt.id === request.operationId &&
    receipt.vehicleId === vehicleId &&
    receipt.publicationId === request.publicationId &&
    receipt.promoId === request.promoId,
  );
}
