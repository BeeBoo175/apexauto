import { authFailure, authJson, authUpstream, upstreamDenial } from "@/lib/auth/http";
import { isRecord } from "@/lib/auth/contracts";

// Whether deposits go through the payment provider. It reveals no setting beyond that one fact.
export async function GET(request: Request) {
  try {
    const response = await authUpstream("/api/payments/mode", { signal: request.signal });
    if (
      response.status === 200 &&
      isRecord(response.body) &&
      typeof response.body.live === "boolean"
    ) {
      return authJson({ live: response.body.live });
    }

    return upstreamDenial(response.status, response.retryAfterSeconds);
  } catch (error) {
    return authFailure(error);
  }
}
