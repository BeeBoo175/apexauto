import { apiError } from "@/lib/server/http/api";
import { requireSameOrigin } from "@/lib/server/http/request";
import { relayReservationCancellation } from "@/lib/vehicles/reservation-routes";

export async function POST(request: Request, context: { params: Promise<{ id: string }> }) {
  try {
    requireSameOrigin(request);
    const { id } = await context.params;
    return await relayReservationCancellation(request, id);
  } catch (error) {
    return apiError(error);
  }
}
