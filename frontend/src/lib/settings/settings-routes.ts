import "server-only";
import { API_DEADLINE_MS } from "@/lib/api";
import { isPositiveInteger, isRecord, isUtcInstant, isUuid } from "@/lib/auth/contracts";
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
import { hasSettingsAccess, warrantyGrades } from "./settings";

export type SettingsResource = "settings" | "promos" | "warranties";

const upstream: Record<SettingsResource, string> = {
  settings: "/api/staff/settings",
  promos: "/api/staff/promos",
  warranties: "/api/staff/warranties",
};

const listKeys = ["page", "pageSize", "search", "status", "sort", "direction"];
// The browser uses one neutral column-filter vocabulary; each upstream resource names its own columns.
const columnKeys: Record<Exclude<SettingsResource, "settings">, Record<string, string>> = {
  promos: {
    text: "code",
    minimum: "minimumDiscount",
    maximum: "maximumDiscount",
    expiresFrom: "expiresFrom",
    expiresTo: "expiresTo",
  },
  warranties: {
    text: "name",
    minimum: "minimumPrice",
    maximum: "maximumPrice",
    grade: "grade",
    termFrom: "minimumTerm",
    termTo: "maximumTerm",
    mileageFrom: "minimumMileage",
    mileageTo: "maximumMileage",
  },
};
type Body = Record<string, unknown>;

async function settingsActor(signal: AbortSignal) {
  const actor = await confirmedActor(signal);
  const denied = !hasSettingsAccess(actor)
    ? accessDenied(actor, "Settings access unavailable.")
    : null;
  return { actor, denied };
}

export async function relaySettingsRead<T>(
  request: Request,
  resource: SettingsResource,
  parse: (value: unknown) => T | null,
  id: string | null = null,
) {
  try {
    if (id !== null && !isUuid(id)) {
      throw new HttpError(400, "Choose a valid record ID.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const current = await settingsActor(signal);
    if (current.denied) {
      return current.denied;
    }
    const source = new URL(request.url).searchParams;
    const allowed = new URLSearchParams();
    if (id === null && resource !== "settings") {
      const listed = allowedQuery(source, listKeys);
      listed.forEach((value, key) => allowed.set(key, value));
      for (const [key, upstreamKey] of Object.entries(columnKeys[resource])) {
        if (source.has(key)) {
          allowed.set(upstreamKey, source.get(key)!);
        }
      }
    }
    const query = allowed.size > 0 ? `?${allowed}` : "";
    const path = id ? `${upstream[resource]}/${id}` : upstream[resource];
    const result = await privateApiRead(`${path}${query}`, parse, signal);
    return apiJson({ ...result, actor: current.actor });
  } catch (error) {
    return apiError(error);
  }
}

export async function relaySettingsWrite<T>(
  request: Request,
  resource: SettingsResource,
  method: "POST" | "PUT",
  id: string | null,
  shape: (body: Body) => Body | null,
  parse: (value: unknown) => T | null,
) {
  try {
    requireSameOrigin(request);
    if (id !== null && !isUuid(id)) {
      throw new HttpError(400, "Choose a valid record ID.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const token = await sessionToken();
    if (!token) {
      throw new HttpError(401, "Sign in to continue.");
    }
    const current = await settingsActor(signal);
    if (current.denied) {
      return current.denied;
    }
    requireJsonRequest(request);
    const body = await readBoundedJson(request.body, signal, 8 * 1024);
    const forwarded = isRecord(body) ? shape(body) : null;
    if (!forwarded) {
      throw new HttpError(400, "Provide valid details within the approved limits.");
    }
    const path = id ? `${upstream[resource]}/${id}` : upstream[resource];
    const result = await requestApiJson(path, parse, { token, signal, method, body: forwarded });
    return apiJson({ ...result, actor: current.actor });
  } catch (error) {
    if (error instanceof SyntaxError) {
      return apiError(new HttpError(400, "Provide valid JSON details."));
    }
    return apiError(error);
  }
}

export function shapeSettings(body: Body) {
  if (
    !isMyr(body.processingFee) ||
    typeof body.sstRate !== "number" ||
    !Number.isFinite(body.sstRate) ||
    !isMyr(body.maxAgentDiscountAmount) ||
    !isPositiveInteger(body.reservationHoldHours) ||
    !isPositiveInteger(body.expectedVersion)
  ) {
    return null;
  }
  return {
    processingFee: body.processingFee,
    sstRate: body.sstRate,
    maxAgentDiscountAmount: body.maxAgentDiscountAmount,
    reservationHoldHours: body.reservationHoldHours,
    expectedVersion: body.expectedVersion,
  };
}

function promoTerms(body: Body) {
  if (
    !isMyr(body.discount) ||
    !isUtcInstant(body.expiresAtUtc) ||
    typeof body.isActive !== "boolean"
  ) {
    return null;
  }
  return { discount: body.discount, expiresAtUtc: body.expiresAtUtc, isActive: body.isActive };
}

export function shapePromoCreate(body: Body) {
  const terms = promoTerms(body);
  if (!terms || !isUuid(body.id) || typeof body.code !== "string" || !body.code.trim()) {
    return null;
  }
  return { id: body.id, code: body.code.trim(), ...terms };
}

export function shapePromoUpdate(body: Body) {
  const terms = promoTerms(body);
  return terms && isPositiveInteger(body.expectedVersion)
    ? { ...terms, expectedVersion: body.expectedVersion }
    : null;
}

function warrantyTerms(body: Body) {
  if (
    typeof body.name !== "string" ||
    !body.name.trim() ||
    !isPositiveInteger(body.durationMonths) ||
    !isPositiveInteger(body.mileageCapKm) ||
    !isMyr(body.price) ||
    !warrantyGrades.some((grade) => grade === body.minimumGrade) ||
    typeof body.isActive !== "boolean"
  ) {
    return null;
  }
  return {
    name: body.name.trim(),
    durationMonths: body.durationMonths,
    mileageCapKm: body.mileageCapKm,
    price: body.price,
    minimumGrade: body.minimumGrade,
    isActive: body.isActive,
  };
}

export function shapeWarrantyCreate(body: Body) {
  const terms = warrantyTerms(body);
  return terms && isUuid(body.id) ? { id: body.id, ...terms } : null;
}

export function shapeWarrantyUpdate(body: Body) {
  const terms = warrantyTerms(body);
  return terms && isPositiveInteger(body.expectedVersion)
    ? { ...terms, expectedVersion: body.expectedVersion }
    : null;
}
