import "server-only";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { readCurrentActor } from "@/lib/auth/server";
import { HttpError, readBoundedJson, requireSameOrigin } from "@/lib/server/http/request";
import { intakeRead, intakeToken } from "@/lib/intake/server";
import { intakeError, intakeJson } from "@/lib/intake/routes";

export function inspectionQuery(request: Request, history = false) {
  const source = new URL(request.url).searchParams;
  const query = new URLSearchParams();
  const fields = history
    ? ["page", "pageSize"]
    : ["page", "pageSize", "search", "status", "from", "to", "branchId"];
  for (const key of fields) {
    if (source.has(key)) {
      query.set(key, source.get(key)!);
    }
  }
  return query.toString();
}

export async function relayInspection<T>(
  request: Request,
  path: string,
  parse: (v: unknown) => T | null,
) {
  try {
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(20_000)]);
    if (request.method !== "GET") {
      requireSameOrigin(request);
    }
    const token = await intakeToken();
    if (!token) {
      throw new HttpError(401, "Sign in to continue.");
    }
    const current = await readCurrentActor(signal);
    if (!current.actor) {
      throw new HttpError(
        current.status === 401 ? 401 : 502,
        "Your session could not be confirmed.",
      );
    }
    let body: unknown;
    if (request.method === "POST") {
      if (request.headers.get("content-type")?.split(";")[0] !== "application/json") {
        throw new HttpError(415, "Send application/json.");
      }
      body = await readBoundedJson(request.body, signal, 512 * 1024);
      if (!isRecord(body) || !isUuid(body.operationId)) {
        throw new HttpError(400, "Provide an operation ID and valid inspection details.");
      }
    }
    const result = await intakeRead(
      `/api/staff/inspection/${path}`,
      parse,
      { token, signal, method: request.method, body },
      20_000,
      1024 * 1024,
    );
    return intakeJson({ ...result, actor: current.actor });
  } catch (error) {
    if (error instanceof SyntaxError) {
      return intakeError(new HttpError(400, "Provide valid JSON details."));
    }
    return intakeError(error);
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
    return intakeError(new HttpError(400, "Choose a valid inspection ID."));
  }
  if (
    revision !== undefined &&
    (!/^\d+$/.test(revision) || !Number.isSafeInteger(Number(revision)) || Number(revision) < 1)
  ) {
    return intakeError(new HttpError(400, "Choose a valid report revision."));
  }
  return relayInspection(request, path, parse);
}
