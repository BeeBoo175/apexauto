import "server-only";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { HttpError, readBoundedJson } from "@/lib/http";
import { requireSameOrigin } from "@/lib/server/http/request";
import { requestApiJson, sessionToken } from "@/lib/server/http/api";
import { apiError, apiJson } from "@/lib/server/http/api";
import { allowedQuery, confirmedActor, requireJsonRequest } from "@/lib/server/http/staff-relay";

export function inspectionQuery(request: Request, history = false) {
  const source = new URL(request.url).searchParams;
  const fields = history
    ? ["page", "pageSize"]
    : [
        "page",
        "pageSize",
        "search",
        "status",
        "from",
        "to",
        "branchId",
        "sort",
        "direction",
        "workStatus",
        "technician",
        "minimumBudget",
        "maximumBudget",
        "registration",
        "vehicle",
      ];
  return allowedQuery(source, fields).toString();
}

export async function relayInspection<T>(
  request: Request,
  path: string,
  parse: (v: unknown) => T | null,
) {
  try {
    const method = request.method === "HEAD" ? "GET" : request.method;
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(20_000)]);
    if (method !== "GET") {
      requireSameOrigin(request);
    }
    const token = await sessionToken();
    if (!token) {
      throw new HttpError(401, "Sign in to continue.");
    }
    const actor = await confirmedActor(signal);
    let body: unknown;
    if (method === "POST") {
      requireJsonRequest(request);
      body = await readBoundedJson(request.body, signal, 512 * 1024);
      if (!isRecord(body) || !isUuid(body.operationId)) {
        throw new HttpError(400, "Provide an operation ID and valid inspection details.");
      }
    }
    const result = await requestApiJson(
      `/api/staff/inspection/${path}`,
      parse,
      { token, signal, method, body },
      20_000,
      1024 * 1024,
    );
    return apiJson({ ...result, actor });
  } catch (error) {
    if (error instanceof SyntaxError) {
      return apiError(new HttpError(400, "Provide valid JSON details."));
    }
    return apiError(error);
  }
}

export function relayInspectionResource<T>(
  request: Request,
  id: string,
  path: string,
  parse: (v: unknown) => T | null,
  revision?: string,
) {
  if (!isUuid(id)) {
    return apiError(new HttpError(400, "Choose a valid inspection ID."));
  }
  if (
    revision !== undefined &&
    (!/^\d+$/.test(revision) || !Number.isSafeInteger(Number(revision)) || Number(revision) < 1)
  ) {
    return apiError(new HttpError(400, "Choose a valid report revision."));
  }
  return relayInspection(request, path, parse);
}
