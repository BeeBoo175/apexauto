import "server-only";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { readCurrentActor } from "@/lib/auth/server";
import { isMyr } from "@/lib/currency-input";
import { HttpError, readBoundedJson } from "@/lib/http";
import { requireSameOrigin } from "@/lib/server/http/request";
import {
  apiError,
  apiJson,
  privateApiRead,
  requestApiJson,
  sessionToken,
} from "@/lib/server/http/api";
import type { CurrentActor } from "@/lib/auth/actor";
import { hasDealAccess } from "./deals";

type Body = Record<string, unknown>;

// Which upstream a relay forwards to, who may use it and which resources it will name.
export interface StaffRelay {
  upstream: string;
  access: (actor: CurrentActor) => boolean;
  reads: RegExp;
  writes?: RegExp;
}

const dealRelay: StaffRelay = {
  upstream: "/api/staff/deals",
  access: hasDealAccess,
  reads: /^\/[0-9a-f-]{36}(?:\/options)?$/i,
};

const listKeys = [
  "page",
  "pageSize",
  "view",
  "status",
  "sort",
  "direction",
  "number",
  "registration",
  "vehicle",
  "customer",
  "search",
  "agent",
  "branch",
  "nextStep",
  "minimumBalance",
  "maximumBalance",
  "minimumDiscount",
  "maximumDiscount",
  "submittedFrom",
  "submittedTo",
  "updatedFrom",
  "updatedTo",
];

// Deal actions the browser may request, each mapped to its upstream route and whether it needs a reason.
export const dealActions = {
  submissions: { reason: false },
  approvals: { reason: false },
  rejections: { reason: true },
  cancellations: { reason: true },
} as const;

async function relayActor(relay: StaffRelay, signal: AbortSignal) {
  const current = await readCurrentActor(signal);
  if (!current.actor) {
    throw new HttpError(current.status === 401 ? 401 : 502, "Your session could not be confirmed.");
  }
  const denied = !relay.access(current.actor)
    ? apiJson({
        status: 403,
        data: null,
        message: "Access unavailable.",
        uncertain: false,
        actor: current.actor,
      })
    : null;
  return { actor: current.actor, denied };
}

export async function relayStaffRead<T>(
  relay: StaffRelay,
  request: Request,
  resource: string,
  parse: (value: unknown) => T | null,
  keys: string[] = [],
) {
  try {
    if (resource && !relay.reads.test(resource)) {
      throw new HttpError(400, "Choose a valid resource.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(25_000)]);
    const current = await relayActor(relay, signal);
    if (current.denied) {
      return current.denied;
    }
    const source = new URL(request.url).searchParams;
    const allowed = new URLSearchParams();
    for (const key of keys) {
      if (source.has(key)) {
        allowed.set(key, source.get(key)!);
      }
    }
    const query = allowed.size > 0 ? `?${allowed}` : "";
    const result = await privateApiRead(`${relay.upstream}${resource}${query}`, parse, signal);
    return apiJson({ ...result, actor: current.actor });
  } catch (error) {
    return apiError(error);
  }
}

export const relayDealRead = <T>(
  request: Request,
  resource: string,
  parse: (value: unknown) => T | null,
  keys: string[] = [],
) => relayStaffRead(dealRelay, request, resource, parse, keys);

export const relayDealList = <T>(request: Request, parse: (value: unknown) => T | null) =>
  relayDealRead(request, "", parse, listKeys);

export async function relayStaffWrite<T>(
  relay: StaffRelay,
  request: Request,
  resource: string,
  method: "POST" | "PUT",
  shape: (body: Body) => Body | null,
  parse: (value: unknown) => T | null,
) {
  try {
    requireSameOrigin(request);
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(25_000)]);
    const token = await sessionToken();
    if (!token) {
      throw new HttpError(401, "Sign in to continue.");
    }
    if (relay.writes && !relay.writes.test(resource)) {
      throw new HttpError(400, "Choose a valid resource.");
    }
    const current = await relayActor(relay, signal);
    if (current.denied) {
      return current.denied;
    }
    if (request.headers.get("content-type")?.split(";")[0] !== "application/json") {
      throw new HttpError(415, "Send application/json.");
    }
    const body = await readBoundedJson(request.body, signal, 8 * 1024);
    const forwarded = isRecord(body) ? shape(body) : null;
    if (!forwarded) {
      throw new HttpError(400, "Provide valid deal details within the approved limits.");
    }
    const result = await requestApiJson(`${relay.upstream}${resource}`, parse, {
      token,
      signal,
      method,
      body: forwarded,
    });
    return apiJson({ ...result, actor: current.actor });
  } catch (error) {
    if (error instanceof SyntaxError) {
      return apiError(new HttpError(400, "Provide valid JSON details."));
    }
    return apiError(error);
  }
}

export const relayDealWrite = <T>(
  request: Request,
  resource: string,
  method: "POST" | "PUT",
  shape: (body: Body) => Body | null,
  parse: (value: unknown) => T | null,
) => relayStaffWrite(dealRelay, request, resource, method, shape, parse);

const version = (value: unknown): value is number =>
  Number.isSafeInteger(value) && Number(value) > 0;
const nullableId = (value: unknown): value is string | null => value === null || isUuid(value);

export function shapeDealRevise(body: Body) {
  const warranty = body.warrantyPackageId ?? null;
  const tradeIn = body.tradeInAppointmentId ?? null;
  if (
    !version(body.expectedVersion) ||
    !nullableId(warranty) ||
    !nullableId(tradeIn) ||
    !isMyr(body.negotiatedDiscount) ||
    !isMyr(body.tradeInAllowance) ||
    (tradeIn === null) !== (body.tradeInAllowance === 0)
  ) {
    return null;
  }
  return {
    expectedVersion: body.expectedVersion,
    warrantyPackageId: warranty,
    negotiatedDiscount: body.negotiatedDiscount,
    tradeInAppointmentId: tradeIn,
    tradeInAllowance: body.tradeInAllowance,
  };
}

export function shapeDealTransition(needsReason: boolean) {
  return (body: Body) => {
    const reason = body.reason ?? null;
    const valid = needsReason
      ? typeof reason === "string" &&
        reason === reason.trim() &&
        reason.length >= 1 &&
        reason.length <= 1000
      : reason === null;
    return version(body.expectedVersion) && valid
      ? { expectedVersion: body.expectedVersion, reason }
      : null;
  };
}
