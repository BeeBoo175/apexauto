// What the deposit payment endpoints answer, mirroring the backend payment callback status names.
const DEPOSIT_PAYMENT_STATUSES = [
  "confirmed",
  "waiting",
  "payment-required",
  "lapsed",
  "conflict",
] as const;

export type DepositPaymentStatus = (typeof DEPOSIT_PAYMENT_STATUSES)[number];

export function parseDepositPaymentStatus(value: unknown): DepositPaymentStatus | null {
  return DEPOSIT_PAYMENT_STATUSES.find((status) => status === value) ?? null;
}
