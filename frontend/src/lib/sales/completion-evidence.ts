import { isNonNegativeInteger, isRecord } from "@/lib/auth/contracts";
import { isMyr } from "@/lib/currency-input";

export const paymentMethods = ["Cash", "BankTransfer", "HirePurchase", "ZeroBalance"] as const;
export type PaymentMethod = (typeof paymentMethods)[number];

export interface SalePaymentEvidence {
  method: PaymentMethod;
  receivedAmount: number;
  paymentReference: string | null;
  paymentDate: string | null;
  bank: string | null;
  financingAmount: number;
  louReference: string | null;
  louVerified: boolean;
  zeroBalanceConfirmed: boolean;
}

export interface SaleHandoverEvidence {
  odometerKilometres: number;
  keysDelivered: number;
  customerAcknowledged: boolean;
  ownershipTransferCompleted: boolean;
  insuranceConfirmed: boolean;
}

export const boundedText = (value: unknown, maximum: number): value is string =>
  typeof value === "string" &&
  value.trim().length > 0 &&
  value.length <= maximum &&
  !/\p{Cc}/u.test(value);

export function isPaymentDate(value: unknown): value is string {
  return (
    typeof value === "string" &&
    /^\d{4}-\d{2}-\d{2}$/.test(value) &&
    Number.isFinite(Date.parse(value)) &&
    new Date(value).toISOString().slice(0, 10) === value
  );
}

export function parsePaymentEvidence(value: unknown): SalePaymentEvidence | null {
  if (!isRecord(value)) {
    return null;
  }
  const method = typeof value.method === "number" ? paymentMethods[value.method] : value.method;
  if (
    !paymentMethods.some((item) => item === method) ||
    !isMyr(value.receivedAmount) ||
    !isMyr(value.financingAmount) ||
    (value.paymentReference !== null && !boundedText(value.paymentReference, 1000)) ||
    (value.paymentDate !== null && !isPaymentDate(value.paymentDate)) ||
    (value.bank !== null && !boundedText(value.bank, 100)) ||
    (value.louReference !== null && !boundedText(value.louReference, 1000)) ||
    typeof value.louVerified !== "boolean" ||
    typeof value.zeroBalanceConfirmed !== "boolean"
  ) {
    return null;
  }
  return { ...value, method } as SalePaymentEvidence;
}

export function parseHandoverEvidence(value: unknown): SaleHandoverEvidence | null {
  if (
    !isRecord(value) ||
    !isNonNegativeInteger(value.odometerKilometres) ||
    !isNonNegativeInteger(value.keysDelivered) ||
    typeof value.customerAcknowledged !== "boolean" ||
    typeof value.ownershipTransferCompleted !== "boolean" ||
    typeof value.insuranceConfirmed !== "boolean"
  ) {
    return null;
  }
  return value as unknown as SaleHandoverEvidence;
}
