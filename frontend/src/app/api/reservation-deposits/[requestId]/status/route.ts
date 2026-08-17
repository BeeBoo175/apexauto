import { authFailure, authJson, authUpstream, upstreamDenial } from "@/lib/auth/http";
import { isRecord, isUuid } from "@/lib/auth/contracts";

const STATUSES = ["confirmed", "waiting", "payment-required", "lapsed", "conflict"];

// The page the customer returns to after paying reads the stored state only; it never asks the payment provider.
export async function GET(request: Request, context: { params: Promise<{ requestId: string }> }) {
  try {
    const { requestId } = await context.params;
    if (!isUuid(requestId)) {
      return authJson({ message: "The deposit request is invalid." }, 400);
    }

    const response = await authUpstream(`/api/reservation-deposits/${requestId}/status`, {
      signal: request.signal,
    });

    if (response.status === 404) {
      return authJson({ message: "The deposit request was not found." }, 404);
    }

    if (
      response.status === 200 &&
      isRecord(response.body) &&
      typeof response.body.status === "string" &&
      STATUSES.includes(response.body.status)
    ) {
      return authJson({ status: response.body.status });
    }

    return upstreamDenial(response.status, response.retryAfterSeconds);
  } catch (error) {
    return authFailure(error);
  }
}
