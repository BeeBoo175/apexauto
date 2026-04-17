import { API_DEADLINE_MS } from "@/lib/api";
import { actorIdentity } from "@/lib/auth/actor";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { readCurrentActor } from "@/lib/auth/server";
import { HttpError, readBoundedJson } from "@/lib/http";
import { apiError, apiJson, requestApiJson, sessionToken } from "@/lib/server/http/api";
import { requireSameOrigin } from "@/lib/server/http/request";
import {
  matchesListingEstimate,
  parseListingEstimate,
  parseListingEstimateAmounts,
} from "@/lib/vehicles/listing-estimate";
import { hasListingAccess } from "@/lib/vehicles/listings";

export async function POST(request: Request, context: { params: Promise<{ id: string }> }) {
  try {
    requireSameOrigin(request);
    const { id } = await context.params;
    if (!isUuid(id)) {
      throw new HttpError(400, "Choose a valid vehicle ID.");
    }
    if (request.headers.get("content-type")?.split(";")[0] !== "application/json") {
      throw new HttpError(415, "Send application/json.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
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
    const body = await readBoundedJson(request.body, signal, 4096);
    const amounts = parseListingEstimateAmounts(body);
    if (!isRecord(body) || !isUuid(body.branchId) || !amounts) {
      throw new HttpError(400, "Provide a branch and exact vehicle price and road tax.");
    }
    if (!hasListingAccess(current.actor) || body.expectedActor !== actorIdentity(current.actor)) {
      return apiJson({
        status: 403,
        data: null,
        message: "Listing access changed. Reopen the workspace.",
        uncertain: false,
        actor: current.actor,
      });
    }
    const expected = {
      ...amounts,
      actorId: current.actor.userId,
      vehicleId: id,
      branchId: body.branchId,
    };
    const result = await requestApiJson(
      `/api/staff/listings/${id}/price-estimates`,
      (value) => {
        const estimate = parseListingEstimate(value);
        return estimate && matchesListingEstimate(estimate, expected) ? estimate : null;
      },
      {
        token,
        signal,
        method: "POST",
        body: { branchId: body.branchId, ...amounts },
      },
      API_DEADLINE_MS,
      4096,
    );
    return apiJson({ ...result, actor: current.actor });
  } catch (error) {
    return apiError(
      error instanceof SyntaxError ? new HttpError(400, "Provide valid JSON details.") : error,
    );
  }
}
