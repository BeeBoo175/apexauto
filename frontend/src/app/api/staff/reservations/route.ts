import { readCurrentActor } from "@/lib/auth/server";
import { apiError, apiJson, privateApiRead } from "@/lib/server/http/api";
import { HttpError } from "@/lib/http";
import { hasReservationAccess } from "@/lib/vehicles/reservations";
import { parseReservations } from "@/lib/vehicles/parse-reservations";

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
    if (!hasReservationAccess(current.actor)) {
      return apiJson({
        status: 403,
        data: null,
        message: "Reservation access unavailable.",
        uncertain: false,
        actor: current.actor,
      });
    }
    const source = new URL(request.url).searchParams;
    const allowed = new URLSearchParams({ isActive: source.get("isActive") ?? "true" });
    for (const key of ["page", "pageSize", "branchId", "sort", "direction"]) {
      if (source.has(key)) {
        allowed.set(key, source.get(key)!);
      }
    }
    const result = await privateApiRead(
      `/api/staff/reservations?${allowed}`,
      parseReservations,
      signal,
    );
    return apiJson({ ...result, actor: current.actor });
  } catch (error) {
    return apiError(error);
  }
}
