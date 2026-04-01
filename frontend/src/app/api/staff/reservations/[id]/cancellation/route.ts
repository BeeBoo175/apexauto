import { isRecord, isUuid } from "@/lib/auth/contracts";
import { readCurrentActor } from "@/lib/auth/server";
import { HttpError, readBoundedJson } from "@/lib/http";
import { requireSameOrigin } from "@/lib/server/http/request";
import { apiError, apiJson, apiRead, sessionToken } from "@/lib/server/http/api";
import { hasReservationAccess } from "@/lib/vehicles/reservations";
import {
  parseReservationCancellation,
  reservationCancellationReason,
} from "@/lib/vehicles/reservation-cancellation";

export async function POST(request: Request, context: { params: Promise<{ id: string }> }) {
  try {
    requireSameOrigin(request);
    const { id } = await context.params;
    if (!isUuid(id)) {
      throw new HttpError(400, "Choose a valid reservation ID.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(25_000)]);
    const token = await sessionToken();
    if (!token) {
      throw new HttpError(401, "Sign in to continue.");
    }
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
    if (request.headers.get("content-type")?.split(";")[0] !== "application/json") {
      throw new HttpError(415, "Send application/json.");
    }
    const body = await readBoundedJson(request.body, signal, 8 * 1024);
    if (
      !isRecord(body) ||
      !isUuid(body.operationId) ||
      typeof body.reason !== "string" ||
      reservationCancellationReason(body.reason) !== body.reason
    ) {
      throw new HttpError(400, "Provide an operation ID and a valid cancellation reason.");
    }
    const result = await apiRead(
      `/api/reservations/${id}/cancellation`,
      parseReservationCancellation,
      {
        token,
        signal,
        method: "POST",
        body: { operationId: body.operationId, reason: body.reason },
      },
    );
    return apiJson({ ...result, actor: current.actor });
  } catch (error) {
    if (error instanceof SyntaxError) {
      return apiError(new HttpError(400, "Provide valid JSON details."));
    }
    return apiError(error);
  }
}
