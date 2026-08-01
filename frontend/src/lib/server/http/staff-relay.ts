import "server-only";
import { API_DEADLINE_MS } from "@/lib/api";
import { parseActor, type CurrentActor } from "@/lib/auth/actor";
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
  if (current.status === 429) {
    throw new HttpError(
      429,
      "Too many requests. Wait before trying again.",
      current.retryAfterSeconds,
    );
  }
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
  // Opt in only after reviewing the backend authorization and the parser's current actor.
  dataFirst?: boolean;
  deadlineMs?: number;
  maximumResponseBytes?: number;
  allowed: (actor: CurrentActor) => boolean;
  denial: string;
  path: string;
  // Browser query keys forwarded upstream; none by default.
  keys?: string[];
  // Receives the forwarded query so the parser can check the answer matches the request.
  parse: (value: unknown, query: URLSearchParams) => unknown;
}

// Actor-bearing reads authorize the parsed backend actor; other reads retain session confirmation.
export async function relayGatedRead(request: Request, read: GatedRead) {
  try {
    const signal = AbortSignal.any([
      request.signal,
      AbortSignal.timeout(read.deadlineMs ?? API_DEADLINE_MS),
    ]);
    const actor = read.dataFirst ? null : await confirmedActor(signal);
    if (actor && !read.allowed(actor)) {
      return accessDenied(actor, read.denial);
    }

    const query = allowedQuery(new URL(request.url).searchParams, read.keys ?? []);
    let deniedActor: CurrentActor | null = null;
    const result = await privateApiRead(
      withQuery(read.path, query),
      (value) => {
        const responseActor = isRecord(value) ? parseActor(value.actor) : null;
        if (read.dataFirst && responseActor && !read.allowed(responseActor)) {
          deniedActor = responseActor;
          return null;
        }
        return read.parse(value, query);
      },
      signal,
      read.maximumResponseBytes,
    );
    if (deniedActor) {
      return accessDenied(deniedActor, read.denial);
    }
    const upstream = isRecord(result.data) ? parseActor(result.data.actor) : null;
    if (read.dataFirst && result.data && !upstream) {
      throw new HttpError(502, "The response actor could not be confirmed.");
    }
    if (read.dataFirst && upstream && !read.allowed(upstream)) {
      return accessDenied(upstream, read.denial);
    }
    return apiJson({ ...result, actor: upstream ?? actor ?? undefined });
  } catch (error) {
    return apiError(error);
  }
}
