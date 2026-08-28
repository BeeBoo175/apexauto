import "server-only";
import { API_DEADLINE_MS } from "@/lib/api";
import { isRecord } from "@/lib/auth/contracts";
import { HttpError, readBoundedJson } from "@/lib/http";
import { apiError, apiJson, sessionToken } from "@/lib/server/http/api";
import { requireSameOrigin } from "@/lib/server/http/request";
import { accessDenied, confirmedActor, readJsonBody } from "@/lib/server/http/staff-relay";
import { requestApi } from "@/lib/server/http/upstream";
import { hasAccountAdministration } from "./staff-accounts";

type Body = Record<string, unknown>;

interface AdministratorCommand {
  path: string;
  method: "POST" | "PUT";
  // Null for a command with no body; otherwise reduces the browser's body to exactly what is forwarded.
  shape: ((body: Body) => Body | null) | null;
  // What a 409 means for this command, by the upstream's code when it sends one.
  conflict: (code: unknown) => string;
}

export const administratorAccess = {
  allowed: hasAccountAdministration,
  denial: "Administrator access required.",
};

// The prologue every administrator command shares: same origin, a signed-in session and a confirmed actor.
export async function administratorSession(request: Request, signal: AbortSignal) {
  requireSameOrigin(request);
  const token = await sessionToken();
  if (!token) {
    throw new HttpError(401, "Sign in to continue.");
  }

  const actor = await confirmedActor(signal);
  return { token, actor, allowed: administratorAccess.allowed(actor) };
}

// Administrator account commands answer with an empty accepted or no-content response, so success is confirmed
// from the status alone and the browser receives a small acknowledgement.
export async function relayAdministratorCommand(request: Request, command: AdministratorCommand) {
  try {
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const { token, actor, allowed } = await administratorSession(request, signal);
    if (!allowed) {
      return accessDenied(actor, administratorAccess.denial);
    }

    let forwarded: Body | undefined;
    if (command.shape) {
      const body = await readJsonBody(request, signal, 8 * 1024);
      const shaped = isRecord(body) ? command.shape(body) : null;
      if (!shaped) {
        throw new HttpError(400, "Provide valid details within the approved limits.");
      }
      forwarded = shaped;
    }
    const { response, signal: upstream } = await requestApi(command.path, API_DEADLINE_MS, {
      token,
      signal,
      method: command.method,
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
      404: "That record could not be found.",
      409: command.conflict(code),
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
