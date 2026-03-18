import { isRecord } from "@/lib/auth/contracts";
import { parseBooking } from "./parse";

export function bookingMatchesRequest(receipt: unknown, request: unknown): boolean {
  const booking = parseBooking(receipt);
  if (!booking || !isRecord(request) || !isRecord(request.vehicle)) {
    return false;
  }
  if (!Array.isArray(request.photoIds) || !isRecord(request.acceptedQuote)) {
    return false;
  }
  const { vehicle, photoIds, acceptedQuote } = request;
  return (
    booking.id === request.id &&
    booking.branchId === request.branchId &&
    Date.parse(booking.slot.startsAt) === Date.parse(String(request.startsAt)) &&
    booking.registrationPlate === String(request.registrationPlate).trim().toUpperCase() &&
    Object.entries(booking.vehicle).every(([key, value]) => value === vehicle[key]) &&
    booking.quote.minValuation === acceptedQuote.minValuation &&
    booking.quote.maxValuation === acceptedQuote.maxValuation &&
    booking.quote.currency === acceptedQuote.currency &&
    photoIds.length === 4 &&
    booking.photos.every((photo) => photoIds.includes(photo.id))
  );
}
