import { reservationFilterKeys } from "@/lib/vehicles/reservations";
import { relayReservationRead } from "@/lib/vehicles/reservation-routes";
import { parseReservations } from "@/lib/vehicles/parse-reservations";

export function GET(request: Request) {
  const source = new URL(request.url).searchParams;
  const allowed = new URLSearchParams({ isActive: source.get("isActive") ?? "true" });
  for (const key of [
    "page",
    "pageSize",
    "branchId",
    "claimed",
    "sort",
    "direction",
    ...reservationFilterKeys,
  ]) {
    if (source.has(key)) {
      allowed.set(key, source.get(key)!);
    }
  }
  return relayReservationRead(request, `?${allowed}`, parseReservations);
}
