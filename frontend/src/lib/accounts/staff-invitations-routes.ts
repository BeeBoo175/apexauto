import "server-only";
import { API_DEADLINE_MS } from "@/lib/api";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { HttpError, readBoundedJson } from "@/lib/http";
import { apiError, apiJson, sessionToken } from "@/lib/server/http/api";
import { requireSameOrigin } from "@/lib/server/http/request";
import { accessDenied, confirmedActor, requireJsonRequest } from "@/lib/server/http/staff-relay";
import { requestApi } from "@/lib/server/http/upstream";
import { hasAccountAdministration } from "./staff-accounts";
import { shapeEmailChange, shapeInvitation } from "./staff-invitations";

type Body = Record<string, unknown>;

const conflictMessages: Record<string, string> = {
  EmailTaken: "An account or pending invitation already uses this email.",
  UsernameTaken: "That username is taken.",
  Unavailable: "This invitation is no longer pending. Refresh the list.",
};

// The upstream answers these commands with an empty accepted or no-content response, so success is
// confirmed from the status alone and the browser receives a small acknowledgement.
async function relayCommand(
  request: Request,
  path: string,
  method: "POST" | "PUT",
  shape: ((body: Body) => Body | null) | null,
) {
  try {
    requireSameOrigin(request);
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const token = await sessionToken();
    if (!token) {
      throw new HttpError(401, "Sign in to continue.");
    }
    const actor = await confirmedActor(signal);
    if (!hasAccountAdministration(actor)) {
      return accessDenied(actor, "Administrator access required.");
    }
    let forwarded: Body | undefined;
    if (shape) {
      requireJsonRequest(request);
      const body = await readBoundedJson(request.body, signal, 8 * 1024);
      const shaped = isRecord(body) ? shape(body) : null;
      if (!shaped) {
        throw new HttpError(400, "Provide valid details within the approved limits.");
      }
      forwarded = shaped;
    }
    const { response, signal: upstream } = await requestApi(path, API_DEADLINE_MS, {
      token,
      signal,
      method,
      body: forwarded,
    });
    if (response.status === 202 || response.status === 204 || response.status === 200) {
      await response.body?.cancel();
      return apiJson({
        status: 200,
        data: { accepted: true },
        message: "",
        uncertain: false,
        actor,
      });
    }
    let code: unknown = null;
    if (response.status === 409) {
      try {
        const value = await readBoundedJson(response.body, upstream, 4 * 1024);
        code = isRecord(value) ? value.code : null;
      } catch {
        code = null;
      }
    } else {
      await response.body?.cancel();
    }
    if (response.status >= 500) {
      throw new Error("Unconfirmed API response");
    }
    const messages: Record<number, string> = {
      400: "Check the details and try again.",
      401: "Sign in to continue.",
      403: "Administrator access required.",
      404: "That invitation could not be found.",
      409: (typeof code === "string" && conflictMessages[code]) || conflictMessages.Unavailable,
      429: "Too many attempts. Wait a moment before trying again.",
    };
    return apiJson({
      status: response.status,
      data: null,
      message: messages[response.status] ?? "The request was not accepted.",
      uncertain: false,
      actor,
    });
  } catch (error) {
    if (error instanceof SyntaxError) {
      return apiError(new HttpError(400, "Provide valid JSON details."));
    }
    return apiError(error);
  }
}

export const relayInvite = (request: Request) =>
  relayCommand(request, "/api/staff-invitations", "POST", shapeInvitation);

function withId(id: string, suffix: string) {
  if (!isUuid(id)) {
    throw new HttpError(400, "Choose a valid invitation.");
  }
  return `/api/staff-invitations/${id}${suffix}`;
}

export function relayInvitationCommand(
  request: Request,
  id: string,
  command: "resends" | "email" | "revocations",
) {
  try {
    if (command === "email") {
      return relayCommand(request, withId(id, "/email"), "PUT", shapeEmailChange);
    }
    return relayCommand(request, withId(id, `/${command}`), "POST", null);
  } catch (error) {
    return Promise.resolve(apiError(error));
  }
}
