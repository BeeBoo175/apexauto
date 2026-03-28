import { isUuid } from "@/lib/auth/contracts";
import { readCurrentActor } from "@/lib/auth/server";
import { apiError, apiJson, privateApiRead } from "@/lib/server/http/api";
import { HttpError } from "@/lib/http";
import { hasListingAccess } from "@/lib/vehicles/listings";
import { parseListingWorkspace } from "@/lib/vehicles/listing-workspace";

export async function GET(request: Request, context: { params: Promise<{ id: string }> }) {
  try {
    const { id } = await context.params;
    if (!isUuid(id)) {
      throw new HttpError(400, "Choose a valid vehicle ID.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(25_000)]);
    const current = await readCurrentActor(signal);
    if (!current.actor) {
      throw new HttpError(
        current.status === 401 ? 401 : 502,
        "Your session could not be confirmed.",
      );
    }
    if (!hasListingAccess(current.actor)) {
      return apiJson({
        status: 403,
        data: null,
        message: "Listing access unavailable.",
        uncertain: false,
        actor: current.actor,
      });
    }
    const result = await privateApiRead(`/api/staff/listings/${id}`, parseListingWorkspace, signal);
    if (result.data && result.data.vehicle.vehicleId !== id) {
      throw new HttpError(502, "The listing response could not be confirmed.");
    }
    return apiJson({ ...result, actor: current.actor });
  } catch (error) {
    return apiError(error);
  }
}
