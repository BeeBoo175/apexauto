import "server-only";
import { API_DEADLINE_MS } from "@/lib/api";
import { isPositiveInteger, isRecord, isUuid } from "@/lib/auth/contracts";
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
import {
  accessDenied,
  allowedQuery,
  confirmedActor,
  requireJsonRequest,
} from "@/lib/server/http/staff-relay";
import { isSaleReason } from "./parse";
import { hasSalesAccess, saleCommands, tableTextKeys, type SaleCommand } from "./contracts";
import { downloadListingPhoto } from "@/lib/vehicles/listing-photo-routes";
import { shapeSettlement, shapeHandover } from "./completion-input";
import { paymentMethods } from "./completion-evidence";

type Body = Record<string, unknown>;

const upstream = "/api/staff/sales";

// The sale reads and commands the browser may name; the backend scopes each to the actor's role, branch and sale.
const saleReads =
  /^(?:|\/queue|\/[0-9a-f-]{36}(?:\/(?:vehicle|options|agents|history|settlement-review|versions\/[1-9]\d{0,9}))?)$/i;
const saleWrites = new RegExp(
  `^/[0-9a-f-]{36}/(?:${saleCommands.join("|")}|document-corrections)$`,
  "i",
);

// Built from the table's filter keys so a new filter reaches the backend without a second list to update.
export const tableKeys = ["page", "pageSize", "view", "sort", "direction", ...tableTextKeys];

export const queueKeys = ["view", "search", "page", "pageSize"];

async function relayActor(signal: AbortSignal) {
  const actor = await confirmedActor(signal);
  const denied = !hasSalesAccess(actor) ? accessDenied(actor, "Access unavailable.") : null;
  return { actor, denied };
}

export async function relaySaleVehiclePhoto(request: Request, saleId: string, photoId: string) {
  try {
    if (!isUuid(saleId) || !isUuid(photoId)) {
      throw new HttpError(400, "Choose a valid sale and photo.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(40_000)]);
    const current = await relayActor(signal);
    if (current.denied) {
      return current.denied;
    }
    const token = await sessionToken();
    if (!token) {
      throw new HttpError(401, "Sign in to continue.");
    }
    return await downloadListingPhoto(
      `${upstream}/${saleId}/vehicle/photos/${photoId}`,
      token,
      signal,
    );
  } catch (error) {
    return apiError(error);
  }
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
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const current = await relayActor(signal);
    if (current.denied) {
      return current.denied;
    }
    const allowed = allowedQuery(new URL(request.url).searchParams, keys);
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
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
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
    requireJsonRequest(request);
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

const nullableId = (value: unknown): value is string | null => value === null || isUuid(value);

// Every command names its operation and the isPositiveInteger it was based on, so a lost response can be replayed.
export function shapeSaleCommand(command: SaleCommand) {
  return (body: Body): Body | null => {
    if (!isUuid(body.operationId) || !isPositiveInteger(body.expectedVersion)) {
      return null;
    }
    const common = { operationId: body.operationId, expectedVersion: body.expectedVersion };
    switch (command) {
      case "assignments":
        return isUuid(body.agentId) ? { ...common, agentId: body.agentId } : null;
      case "settlements": {
        const fields = shapeSettlement(body);
        return fields
          ? {
              ...common,
              ...fields,
              payment: { ...fields.payment, method: paymentMethods.indexOf(fields.payment.method) },
            }
          : null;
      }
      case "handovers": {
        const fields = shapeHandover(body);
        return fields ? { ...common, ...fields } : null;
      }
      case "rejections":
      case "cancellations":
        return isSaleReason(body.reason) ? { ...common, reason: body.reason } : null;
      case "term-revisions": {
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
