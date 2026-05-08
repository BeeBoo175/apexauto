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
import { hasSalesAccess, tableTextKeys } from "./sales";

type Body = Record<string, unknown>;

const upstream = "/api/staff/sales";

// The sale reads and commands the browser may name; the backend scopes each to the actor's role, branch and sale.
const saleReads =
  /^(?:|\/queue|\/[0-9a-f-]{36}(?:\/(?:options|agents|history|versions\/[1-9]\d{0,9}))?)$/i;
const saleWrites =
  /^\/[0-9a-f-]{36}\/(?:claims|assignments|releases|terms|submissions|approvals|rejections|cancellations)$/i;

export const saleCommands = [
  "claims",
  "assignments",
  "releases",
  "terms",
  "submissions",
  "approvals",
  "rejections",
  "cancellations",
] as const;

export type SaleCommand = (typeof saleCommands)[number];

// Built from the table's filter keys so a new filter reaches the backend without a second list to update.
export const tableKeys = ["page", "pageSize", "view", "sort", "direction", ...tableTextKeys];

export const queueKeys = ["view", "search", "page", "pageSize"];

async function relayActor(signal: AbortSignal) {
  const current = await readCurrentActor(signal);
  if (!current.actor) {
    throw new HttpError(current.status === 401 ? 401 : 502, "Your session could not be confirmed.");
  }
  const denied = !hasSalesAccess(current.actor)
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

export async function relaySaleRead<T>(
  request: Request,
  resource: string,
  parse: (value: unknown) => T | null,
  keys: string[] = [],
) {
  try {
    if (!saleReads.test(resource)) {
      throw new HttpError(400, "Choose a valid resource.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(25_000)]);
    const current = await relayActor(signal);
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

export async function relaySaleCommand<T>(
  request: Request,
  resource: string,
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
    if (!saleWrites.test(resource)) {
      throw new HttpError(400, "Choose a valid resource.");
    }
    const current = await relayActor(signal);
    if (current.denied) {
      return current.denied;
    }
    if (request.headers.get("content-type")?.split(";")[0] !== "application/json") {
      throw new HttpError(415, "Send application/json.");
    }
    const body = await readBoundedJson(request.body, signal, 8 * 1024);
    const forwarded = isRecord(body) ? shape(body) : null;
    if (!forwarded) {
      throw new HttpError(400, "Provide valid sale details within the approved limits.");
    }
    const result = await requestApiJson(`${upstream}${resource}`, parse, {
      token,
      signal,
      method: "POST",
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
const validReason = (value: unknown): value is string =>
  typeof value === "string" && value === value.trim() && value.length >= 1 && value.length <= 1000;

// Every command names its operation and the version it was based on, so a lost response can be replayed.
export function shapeSaleCommand(command: SaleCommand) {
  return (body: Body): Body | null => {
    if (!isUuid(body.operationId) || !version(body.expectedVersion)) {
      return null;
    }
    const common = { operationId: body.operationId, expectedVersion: body.expectedVersion };
    switch (command) {
      case "assignments":
        return isUuid(body.agentId) ? { ...common, agentId: body.agentId } : null;
      case "rejections":
      case "cancellations":
        return validReason(body.reason) ? { ...common, reason: body.reason } : null;
      case "terms": {
        const warranty = body.warrantyPackageId ?? null;
        const tradeIn = body.tradeInAppointmentId ?? null;
        if (
          !nullableId(warranty) ||
          !nullableId(tradeIn) ||
          !isMyr(body.negotiatedDiscount) ||
          !isMyr(body.tradeInAllowance) ||
          (tradeIn === null) !== (body.tradeInAllowance === 0)
        ) {
          return null;
        }
        return {
          ...common,
          warrantyPackageId: warranty,
          negotiatedDiscount: body.negotiatedDiscount,
          tradeInAppointmentId: tradeIn,
          tradeInAllowance: body.tradeInAllowance,
        };
      }
      default:
        return common;
    }
  };
}
