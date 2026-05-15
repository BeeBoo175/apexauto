import "server-only";
import type { CurrentActor } from "@/lib/auth/actor";
import { readCurrentActor } from "@/lib/auth/server";
import { HttpError } from "@/lib/http";
import { apiJson } from "./api";

// Each feature relay keeps its own access predicate, body limit and shaping; only these mechanics are shared.

export async function confirmedActor(
  signal: AbortSignal,
  message = "Your session could not be confirmed.",
): Promise<CurrentActor> {
  const current = await readCurrentActor(signal);
  if (!current.actor) {
    throw new HttpError(current.status === 401 ? 401 : 502, message);
  }
  return current.actor;
}

export function accessDenied(actor: CurrentActor, message: string) {
  return apiJson({ status: 403, data: null, message, uncertain: false, actor });
}

export function requireJsonRequest(request: Request) {
  if (request.headers.get("content-type")?.split(";")[0] !== "application/json") {
    throw new HttpError(415, "Send application/json.");
  }
}

// Copies only the named browser query keys, so the upstream never receives an unreviewed parameter.
export function allowedQuery(source: URLSearchParams, keys: string[]) {
  const allowed = new URLSearchParams();
  for (const key of keys) {
    if (source.has(key)) {
      allowed.set(key, source.get(key)!);
    }
  }
  return allowed;
}
