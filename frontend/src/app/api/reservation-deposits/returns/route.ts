import { authFailure, authJson, authUpstream, upstreamDenial } from "@/lib/auth/http";
import { isRecord } from "@/lib/auth/contracts";
import { requireSameOrigin } from "@/lib/server/http/request";

const STATUSES = ["confirmed", "waiting", "payment-required", "lapsed", "conflict"];
const FIELD_LIMIT = 16;

// The browser brings back the provider's signed fields from the payment page; the API verifies the signature and
// confirms the deposit, so the customer does not wait for the provider's own callback.
export async function POST(request: Request) {
  try {
    requireSameOrigin(request);
    const body: unknown = await request.json().catch(() => null);
    if (!isRecord(body) || !isRecord(body.fields)) {
      return authJson({ message: "The payment return is invalid." }, 400);
    }

    const entries = Object.entries(body.fields);
    const valid =
      entries.length > 0 &&
      entries.length <= FIELD_LIMIT &&
      entries.every(
        ([name, value]) =>
          name.startsWith("billplz[") && typeof value === "string" && value.length <= 128,
      );
    if (!valid) {
      return authJson({ message: "The payment return is invalid." }, 400);
    }

    const response = await authUpstream("/api/payments/billplz/returns", {
      method: "POST",
      body: { fields: body.fields },
      signal: request.signal,
    });

    if (
      response.status === 200 &&
      isRecord(response.body) &&
      typeof response.body.status === "string" &&
      STATUSES.includes(response.body.status)
    ) {
      return authJson({ status: response.body.status });
    }

    if (response.status === 400) {
      return authJson({ message: "The payment return is invalid." }, 400);
    }

    return upstreamDenial(response.status, response.retryAfterSeconds);
  } catch (error) {
    return authFailure(error);
  }
}
