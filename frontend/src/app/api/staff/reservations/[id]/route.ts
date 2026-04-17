import { isUuid } from "@/lib/auth/contracts";
import { apiError } from "@/lib/server/http/api";
import { HttpError } from "@/lib/http";
import { relayReservationRead } from "@/lib/vehicles/reservation-routes";
import { parseReservationDetail } from "@/lib/vehicles/reservation-detail";

export async function GET(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  if (!isUuid(id)) {
    return apiError(new HttpError(400, "Choose a valid reservation ID."));
  }
  return relayReservationRead(
    request,
    `/${id}`,
    parseReservationDetail,
    (value) => value.reservation.id === id,
  );
}
