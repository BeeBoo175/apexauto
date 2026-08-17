import { isRecord, isUtcInstant, isUuid } from "@/lib/auth/contracts";

export type DepositRefundState = "due" | "processing" | "completed" | "failed";
export type DepositRefundReason = "released" | "unmatched";

export interface RefundBank {
  code: string;
  name: string;
}

export interface DepositRefund {
  paymentRequestId: string;
  amountSen: number;
  reason: DepositRefundReason;
  vehicle: string;
  state: DepositRefundState;
  bankCode: string | null;
  accountLast4: string | null;
  dueAt: string;
}

export interface DepositRefundList {
  enabled: boolean;
  banks: RefundBank[];
  refunds: DepositRefund[];
}

export interface BankAccountDetails {
  bankCode: string;
  accountNumber: string;
  accountHolder: string;
}

const STATES: DepositRefundState[] = ["due", "processing", "completed", "failed"];

// The API serialises the reason as its enum number: 0 is a released reservation, 1 an unmatched payment.
function parseReason(value: unknown): DepositRefundReason | null {
  if (value === 0 || value === "ReservationReleased") {
    return "released";
  }

  return value === 1 || value === "PaymentUnmatched" ? "unmatched" : null;
}

function parseBank(value: unknown): RefundBank | null {
  return isRecord(value) && typeof value.code === "string" && typeof value.name === "string"
    ? { code: value.code, name: value.name }
    : null;
}

function parseRefund(value: unknown): DepositRefund | null {
  if (
    !isRecord(value) ||
    !isUuid(value.paymentRequestId) ||
    !Number.isSafeInteger(value.amountSen) ||
    (value.amountSen as number) <= 0 ||
    typeof value.vehicle !== "string" ||
    typeof value.state !== "string" ||
    !STATES.includes(value.state as DepositRefundState) ||
    !isUtcInstant(value.dueAt)
  ) {
    return null;
  }

  const reason = parseReason(value.reason);
  if (!reason) {
    return null;
  }

  return {
    paymentRequestId: value.paymentRequestId,
    amountSen: value.amountSen as number,
    reason,
    vehicle: value.vehicle,
    state: value.state as DepositRefundState,
    bankCode: typeof value.bankCode === "string" ? value.bankCode : null,
    accountLast4: typeof value.accountLast4 === "string" ? value.accountLast4 : null,
    dueAt: value.dueAt,
  };
}

export function parseDepositRefundList(value: unknown): DepositRefundList | null {
  if (
    !isRecord(value) ||
    typeof value.enabled !== "boolean" ||
    !Array.isArray(value.banks) ||
    !Array.isArray(value.refunds)
  ) {
    return null;
  }

  const banks = value.banks.map(parseBank);
  const refunds = value.refunds.map(parseRefund);
  if (banks.includes(null) || refunds.includes(null)) {
    return null;
  }

  return {
    enabled: value.enabled,
    banks: banks as RefundBank[],
    refunds: refunds as DepositRefund[],
  };
}

// The answer to a bank account submission: the refund as it now stands.
export const parseDepositRefundReceipt = parseRefund;

export function parseBankAccountRequest(value: unknown): BankAccountDetails | null {
  if (
    !isRecord(value) ||
    typeof value.bankCode !== "string" ||
    !/^[A-Za-z0-9]{1,16}$/.test(value.bankCode) ||
    typeof value.accountNumber !== "string" ||
    !/^[0-9]{6,20}$/.test(value.accountNumber) ||
    typeof value.accountHolder !== "string" ||
    !value.accountHolder.trim() ||
    value.accountHolder.length > 100
  ) {
    return null;
  }

  return {
    bankCode: value.bankCode,
    accountNumber: value.accountNumber,
    accountHolder: value.accountHolder.trim(),
  };
}
