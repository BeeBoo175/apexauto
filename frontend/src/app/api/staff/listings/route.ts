import { readCurrentActor } from "@/lib/auth/server";
import { apiError, apiJson, privateApiRead } from "@/lib/server/http/api";
import { HttpError } from "@/lib/http";
import { hasListingAccess } from "@/lib/vehicles/listings";
import { parseListings } from "@/lib/vehicles/parse-listings";

export async function GET(request: Request) {
  try {
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
    const source = new URL(request.url).searchParams;
    const allowed = new URLSearchParams();
    for (const key of ["page", "pageSize", "search", "branchId"]) {
      if (source.has(key)) {
        allowed.set(key, source.get(key)!);
      }
    }
    const result = await privateApiRead(`/api/staff/listings?${allowed}`, parseListings, signal);
    return apiJson({ ...result, actor: current.actor });
  } catch (error) {
    return apiError(error);
  }
}
