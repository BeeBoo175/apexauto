import "server-only";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { HttpError } from "@/lib/http";
import { apiError, apiJson, sessionToken } from "@/lib/server/http/api";
import { requireSameOrigin } from "@/lib/server/http/request";
import { confirmedActor } from "@/lib/server/http/staff-relay";
import { requestApi } from "@/lib/server/http/upstream";

const USERNAME_DEADLINE_MS = 15_000;
const MAX_BODY_CHARACTERS = 512;

// Relays the self-service username change; the backend answers 204, 400 or 409 with a plain message.
export async function changeUsername(request: Request) {
  try {
    requireSameOrigin(request);
    const token = await sessionToken();
    if (!token) {
      throw new HttpError(401, "Sign in to continue.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(USERNAME_DEADLINE_MS)]);
    const actor = await confirmedActor(signal);
    const text = await request.text();
    const body: unknown = text.length <= MAX_BODY_CHARACTERS ? JSON.parse(text) : null;
    if (!isRecord(body) || !isUuid(body.operationId) || typeof body.username !== "string") {
      throw new HttpError(400, "Enter a valid username.");
    }
    const { response } = await requestApi("/api/account/username", USERNAME_DEADLINE_MS, {
      token,
      signal,
      method: "PUT",
      bytes: new TextEncoder().encode(JSON.stringify(body)),
      contentType: "application/json",
    });
    const reply: unknown = await response.json().catch(() => null);
    if (response.status >= 500) {
      throw new Error("Unconfirmed username change");
    }
    const changed = response.status === 204;
    return apiJson({
      status: changed ? 200 : response.status,
      data: changed ? { username: body.username } : null,
      message: changed ? "" : messageOf(reply),
      uncertain: false,
      actor,
    });
  } catch (error) {
    return apiError(error);
  }
}

function messageOf(reply: unknown) {
  return isRecord(reply) && typeof reply.message === "string"
    ? reply.message
    : "The username could not be changed.";
}
