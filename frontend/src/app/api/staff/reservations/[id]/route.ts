import { isUuid } from "@/lib/auth/contracts";
import { readCurrentActor } from "@/lib/auth/server";
import { apiError, apiJson, privateApiRead } from "@/lib/server/http/api";
import { HttpError } from "@/lib/http";
import { hasReservationAccess } from "@/lib/vehicles/reservations";
import { parseReservationDetail } from "@/lib/vehicles/reservation-detail";

export async function GET(request: Request, context: { params: Promise<{ id: string }> }) {
  try {
    const { id } = await context.params;
    if (!isUuid(id)) {
      throw new HttpError(400, "Choose a valid reservation ID.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(25_000)]);
    const current = await readCurrentActor(signal);
    if (!current.actor) {
      throw new HttpError(
        current.status === 401 ? 401 : 502,
        "Your session could not be confirmed.",
      );
    }
    if (!hasReservationAccess(current.actor)) {
      return apiJson({
        status: 403,
        data: null,
        message: "Reservation access unavailable.",
        uncertain: false,
        actor: current.actor,
      });
    }
    const result = await privateApiRead(
      `/api/staff/reservations/${id}`,
      parseReservationDetail,
      signal,
    );
    if (result.data && result.data.reservation.id !== id) {
      throw new HttpError(502, "The reservation response could not be confirmed.");
    }
    return apiJson({ ...result, actor: current.actor });
  } catch (error) {
    return apiError(error);
  }
}
