import "server-only";
import { API_DEADLINE_MS } from "@/lib/api";
import type { CurrentActor } from "@/lib/auth/actor";
import { isRecord } from "@/lib/auth/contracts";
import { readCurrentActor } from "@/lib/auth/server";
import { HttpError } from "@/lib/http";
import { withQuery } from "@/lib/query";
import { apiError, apiJson, privateApiRead } from "./api";

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

export interface GatedRead {
  allowed: (actor: CurrentActor) => boolean;
  denial: string;
  path: string;
  // Browser query keys forwarded upstream; none by default.
  keys?: string[];
  // Receives the forwarded query so the parser can check the answer matches the request.
  parse: (value: unknown, query: URLSearchParams) => unknown;
}

// The shared mechanics of a staff read relay: deadline, confirmed actor, access predicate, reviewed query and the
// actor the upstream answered with (the confirmed one when the body carries none).
export async function relayGatedRead(request: Request, read: GatedRead) {
  try {
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const actor = await confirmedActor(signal);
    if (!read.allowed(actor)) {
      return accessDenied(actor, read.denial);
    }

    const query = allowedQuery(new URL(request.url).searchParams, read.keys ?? []);
    const result = await privateApiRead(
      withQuery(read.path, query),
      (value) => read.parse(value, query),
      signal,
    );
    const upstream = isRecord(result.data)
      ? (result.data.actor as CurrentActor | undefined)
      : undefined;
    return apiJson({ ...result, actor: upstream ?? actor });
  } catch (error) {
    return apiError(error);
  }
}
