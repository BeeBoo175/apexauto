import { API_DEADLINE_MS } from "@/lib/api";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { HttpError, readBoundedJson } from "@/lib/http";
import { apiError, apiJson, requestApiJson } from "@/lib/server/http/api";
import { requireSameOrigin } from "@/lib/server/http/request";
import { parsePurchaseQuote, promoCode } from "@/lib/vehicles/purchase-quote";

export async function POST(request: Request) {
  try {
    requireSameOrigin(request);
    if (request.headers.get("content-type")?.split(";")[0] !== "application/json") {
      throw new HttpError(415, "Send application/json.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const body = await readBoundedJson(request.body, signal, 4096);
    const code = isRecord(body) && typeof body.code === "string" ? promoCode(body.code) : null;
    if (!isRecord(body) || !isUuid(body.vehicleId) || !code) {
      throw new HttpError(400, "Provide a vehicle and one promo code.");
    }
    return apiJson(
      await requestApiJson("/api/promos/validations", (value) => parsePurchaseQuote(value, code), {
        method: "POST",
        body: { vehicleId: body.vehicleId, code },
        signal,
      }),
    );
  } catch (error) {
    return apiError(
      error instanceof SyntaxError ? new HttpError(400, "Provide valid JSON.") : error,
    );
  }
}
