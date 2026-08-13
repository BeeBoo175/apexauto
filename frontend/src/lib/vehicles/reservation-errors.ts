import type { ApiConflict } from "@/lib/api";

const reservationConflictMessages = {
  "customer-has-reservation":
    "You already have an active reservation. Review it in your account or contact our sales team before reserving another car.",
  "vehicle-reserved":
    "This car has just been reserved by another customer. Choose another car from the catalogue.",
};

// Sales staff reserving for a customer read the same outcomes about the customer, never in the second person.
const assistedReservationConflictMessages: Partial<
  Record<keyof typeof reservationConflictMessages, string>
> = {
  "customer-has-reservation":
    "This customer already has an active reservation. Review it in their account or release it before reserving another car.",
};

export function parseReservationConflict(
  value: unknown,
  assisted = false,
): ApiConflict | undefined {
  if (typeof value !== "string" || !Object.hasOwn(reservationConflictMessages, value)) {
    return undefined;
  }
  const code = value as keyof typeof reservationConflictMessages;
  const message =
    (assisted ? assistedReservationConflictMessages[code] : undefined) ??
    reservationConflictMessages[code];
  return { code, message };
}
