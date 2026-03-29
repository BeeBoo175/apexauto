import { isUuid } from "@/lib/auth/contracts";
import { readCurrentActor } from "@/lib/auth/server";
import { apiError, apiJson, privateApiRead } from "@/lib/server/http/api";
import { HttpError } from "@/lib/http";
import { hasListingAccess } from "@/lib/vehicles/listings";
import { parseListingPhotos } from "@/lib/vehicles/listing-photos";
import { paginationQuery } from "@/lib/pagination";

export async function GET(request: Request, context: { params: Promise<{ id: string }> }) {
  try {
    const { id } = await context.params;
    const input = new URL(request.url).searchParams.get("page") ?? "1";
    const page = Number(input);
    if (!isUuid(id) || !/^\d+$/.test(input) || !Number.isSafeInteger(page) || page < 1) {
      throw new HttpError(400, "Choose a valid vehicle ID and photo page.");
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
    const result = await privateApiRead(
      `/api/staff/listings/${id}/photos?${paginationQuery(page, 10)}`,
      parseListingPhotos,
      signal,
    );
    if (result.data && (result.data.page !== page || result.data.pageSize !== 10)) {
      throw new HttpError(502, "The photo page could not be confirmed.");
    }
    return apiJson({ ...result, actor: current.actor });
  } catch (error) {
    return apiError(error);
  }
}
