import "server-only";
import { cookies } from "next/headers";
import { SESSION_COOKIE } from "@/lib/auth/contracts";
import { requestApi, type ApiRequestOptions } from "@/lib/server/http/upstream";
import { HttpError, readBoundedJson } from "@/lib/http";
import { API_DEADLINE_MS } from "@/lib/api";
import type { ApiResult } from "@/lib/api";

const messages: Record<number, string> = {
  400: "Check the entered details and try again.",
  401: "Your session has ended. Sign in again.",
  403: "This account cannot perform this action.",
  404: "This record is unavailable to your account.",
  409: "The request conflicts with the current record. Refresh the available choices.",
  410: "This photo is pending deletion or has been removed. Choose a new photo.",
  413: "The request or response is too large.",
  415: "Choose a JPEG, PNG or WebP photo.",
  429: "Too many requests. Wait before trying again.",
};

export async function apiRead<T>(
  path: string,
  parse: (value: unknown) => T | null,
  options: ApiRequestOptions = {},
  deadline = API_DEADLINE_MS,
  maximumResponseBytes = 256 * 1024,
): Promise<ApiResult<T>> {
  try {
    const { response, signal } = await requestApi(path, deadline, options);
    if (
      response.status === 401 ||
      response.status === 403 ||
      response.status === 404 ||
      response.status === 429
    ) {
      await response.body?.cancel();
      return {
        status: response.status,
        data: null,
        message: messages[response.status],
        uncertain: false,
      };
    }
    if (response.status >= 500 || response.status === 408) {
      throw new Error("Unconfirmed intake response");
    }
    let data: T | null = null;
    try {
      data = parse(await readBoundedJson(response.body, signal, maximumResponseBytes));
    } catch (error) {
      if (response.ok) {
        throw error;
      }
    }
    if (response.ok && !data) {
      throw new Error("Invalid intake response");
    }
    return {
      status: response.status,
      data,
      message: response.ok ? "" : (messages[response.status] ?? "The request was not accepted."),
      uncertain: false,
    };
  } catch {
    return {
      status: 502,
      data: null,
      message:
        "The service response could not be confirmed. Keep your details and retry deliberately.",
      uncertain: true,
    };
  }
}

export async function sessionToken() {
  return (await cookies()).get(SESSION_COOKIE)?.value;
}

export async function privateApiRead<T>(
  path: string,
  parse: (value: unknown) => T | null,
  signal?: AbortSignal,
  maximumResponseBytes = 256 * 1024,
) {
  const token = await sessionToken();
  if (!token) {
    return { status: 401, data: null, message: messages[401], uncertain: false };
  }
  return apiRead(path, parse, { token, signal }, API_DEADLINE_MS, maximumResponseBytes);
}

export function apiJson<T>(result: ApiResult<T>) {
  return Response.json(result, {
    status: result.status,
    headers: {
      "Cache-Control": "no-store",
      "X-Content-Type-Options": "nosniff",
      "X-Robots-Tag": "noindex, nofollow",
    },
  });
}

export function apiError(error: unknown) {
  const status = error instanceof HttpError ? error.status : 502;
  return apiJson({
    status,
    data: null,
    message:
      error instanceof HttpError
        ? error.message
        : "The result is unconfirmed. Retry the same request.",
    uncertain: status >= 500,
  });
}
