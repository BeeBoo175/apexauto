// Presentation and receipt validation mirror the backend's fixed reservation terms.
export const RESERVATION_DEPOSIT = 500;
export const DEPOSIT_REQUEST_HOURS = 24;
export const PAYMENT_WINDOW_MINUTES = 15;
// The documented default hold length for pages that do not read the live purchase guide.
export const RESERVATION_HOLD_HOURS = 48;

const MS_PER_HOUR = 3_600_000;

// The hold length a reservation was actually given, in whole hours, from its own timestamps.
export function reservationHoldHours(receipt: { createdAt: string; expiresAt: string }) {
  return Math.round((Date.parse(receipt.expiresAt) - Date.parse(receipt.createdAt)) / MS_PER_HOUR);
}
