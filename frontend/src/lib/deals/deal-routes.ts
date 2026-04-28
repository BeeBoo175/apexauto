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
import { hasDealAccess } from "./deals";

type Body = Record<string, unknown>;

const upstream = "/api/staff/deals";
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

async function dealActor(signal: AbortSignal) {
  const current = await readCurrentActor(signal);
  if (!current.actor) {
    throw new HttpError(current.status === 401 ? 401 : 502, "Your session could not be confirmed.");
  }
  const denied = !hasDealAccess(current.actor)
    ? apiJson({
        status: 403,
        data: null,
        message: "Deal access unavailable.",
        uncertain: false,
        actor: current.actor,
      })
    : null;
  return { actor: current.actor, denied };
}

export async function relayDealRead<T>(
  request: Request,
  resource: string,
  parse: (value: unknown) => T | null,
  keys: string[] = [],
) {
  try {
    if (resource && !/^(?:\/vehicles|\/[0-9a-f-]{36}(?:\/options)?)$/i.test(resource)) {
      throw new HttpError(400, "Choose a valid deal resource.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(25_000)]);
    const current = await dealActor(signal);
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
    const result = await privateApiRead(`${upstream}${resource}${query}`, parse, signal);
    return apiJson({ ...result, actor: current.actor });
  } catch (error) {
    return apiError(error);
  }
}

export const relayDealList = <T>(request: Request, parse: (value: unknown) => T | null) =>
  relayDealRead(request, "", parse, listKeys);

export async function relayDealWrite<T>(
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
    const current = await dealActor(signal);
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
    const result = await requestApiJson(`${upstream}${resource}`, parse, {
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

const version = (value: unknown): value is number =>
  Number.isSafeInteger(value) && Number(value) > 0;
const nullableId = (value: unknown): value is string | null => value === null || isUuid(value);

export function shapeDealCreate(body: Body) {
  const email = typeof body.customerEmail === "string" ? body.customerEmail.trim() : null;
  const reservation = body.reservationId ?? null;
  // The customer comes from exactly one source: the reservation or an exact account email.
  if (
    !isUuid(body.id) ||
    !isUuid(body.vehicleId) ||
    !nullableId(reservation) ||
    Boolean(email) === (reservation !== null) ||
    (email !== null && (email.length === 0 || email.length > 256))
  ) {
    return null;
  }
  return {
    id: body.id,
    vehicleId: body.vehicleId,
    reservationId: reservation,
    customerEmail: email,
  };
}

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
