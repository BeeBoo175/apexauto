import type { ApiConflict } from "@/lib/api";

const reservationConflictMessages = {
  "customer-has-reservation":
    "You already have an active reservation. Review it in your account or contact our sales team before reserving another car.",
  "vehicle-reserved":
    "This car has just been reserved by another customer. Choose another car from the catalogue.",
};

export function parseReservationConflict(value: unknown): ApiConflict | undefined {
  if (typeof value !== "string" || !Object.hasOwn(reservationConflictMessages, value)) {
    return undefined;
  }
  const code = value as keyof typeof reservationConflictMessages;
  return { code, message: reservationConflictMessages[code] };
}
