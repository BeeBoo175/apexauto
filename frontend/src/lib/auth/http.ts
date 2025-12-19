import "server-only";

import { headers } from "next/headers";
import { trustedClientAddress } from "./client-address";
import { AUTH_BODY_LIMIT, AUTH_DEADLINE_MS, isRecord, isUuid } from "./contracts";

export class AuthHttpError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
  }
}

export function authJson(body: unknown, status = 200) {
  return Response.json(body, { status, headers: { "Cache-Control": "no-store" } });
}

// Stream chunks up to AUTH_BODY_LIMIT to prevent memory exhaustion on unbuffered payloads.
export async function readBoundedJson(
  body: ReadableStream<Uint8Array> | null,
  signal: AbortSignal,
): Promise<unknown> {
  if (!body) {
    throw new AuthHttpError(400, "A JSON body is required.");
  }

  const reader = body.getReader();
  const chunks: Uint8Array[] = [];
  let size = 0;
  const cancel = () => {
    void reader.cancel().catch(() => {});
  };
  signal.addEventListener("abort", cancel, { once: true });

  try {
    signal.throwIfAborted();

    while (true) {
      const chunk = await reader.read();
      signal.throwIfAborted();

      if (chunk.done) {
        break;
      }

      size += chunk.value.byteLength;

      if (size > AUTH_BODY_LIMIT) {
        throw new AuthHttpError(413, "The request or response is too large.");
      }

      chunks.push(chunk.value);
    }

    return JSON.parse(Buffer.concat(chunks).toString("utf8"));
  } finally {
    await reader.cancel().catch(() => {});
    reader.releaseLock();
    signal.removeEventListener("abort", cancel);
  }
}

// Defense-in-depth CSRF guard verifying exact origin and blocking cross-site callers.
export function requireSameOrigin(request: Request) {
  const configured = process.env.APP_BASE_URL;

  if (!configured) {
    throw new Error("APP_BASE_URL is required.");
  }

  if (
    request.headers.get("origin") !== new URL(configured).origin ||
    request.headers.get("sec-fetch-site") === "cross-site"
  ) {
    throw new AuthHttpError(403, "This action requires a same-origin request.");
  }
}

// Validate payload shape, whitelist allowed fields, and require UUID idempotency keys.
export async function readAuthRequest(
  request: Request,
  kind: "login" | "register" | "logout" | "verify-email" | "resend-verification",
) {
  requireSameOrigin(request);

  if (
    request.headers.get("content-type")?.split(";")[0].trim().toLowerCase() !== "application/json"
  ) {
    throw new AuthHttpError(415, "Send application/json.");
  }

  if (Number(request.headers.get("content-length")) > AUTH_BODY_LIMIT) {
    throw new AuthHttpError(413, "The request is too large.");
  }

  const signal = AbortSignal.any([request.signal, AbortSignal.timeout(AUTH_DEADLINE_MS)]);
  let value: unknown;

  try {
    value = await readBoundedJson(request.body, signal);
  } catch (error) {
    if (error instanceof AuthHttpError) {
      throw error;
    }

    throw new AuthHttpError(400, "The JSON request could not be read.");
  }

  if (kind === "logout") {
    if (!isRecord(value) || Object.keys(value).length !== 0) {
      throw new AuthHttpError(400, "Sign out requires an empty JSON object.");
    }

    return {};
  }

  if (kind === "verify-email") {
    if (!isRecord(value) || typeof value.token !== "string" || value.token.length > 160) {
      throw new AuthHttpError(400, "The verification link is invalid.");
    }

    return { token: value.token };
  }

  if (kind === "resend-verification") {
    if (!isRecord(value) || !isUuid(value.resendKey) || Object.keys(value).length !== 1) {
      throw new AuthHttpError(400, "The resend request is invalid.");
    }

    return { resendKey: value.resendKey };
  }

  const key = kind === "login" ? "loginKey" : "registrationKey";
  const fields =
    kind === "login"
      ? ["email", "password"]
      : ["legalName", "email", "password", "nric", "mobile", "residentialAddress"];

  if (
    !isRecord(value) ||
    !isUuid(value[key]) ||
    fields.some((field) => typeof value[field] !== "string")
  ) {
    throw new AuthHttpError(400, "The authentication request is invalid.");
  }

  return Object.fromEntries([key, ...fields].map((field) => [field, value[field]]));
}

// Upstream API bridge; attaches trusted client IP and disables redirects to protect credentials.
export async function authUpstream(
  path: string,
  options: { body?: unknown; token?: string; signal?: AbortSignal; method?: string } = {},
) {
  const origin = process.env.API_BASE_URL;

  if (!origin) {
    throw new Error("API_BASE_URL is required.");
  }

  const signal = AbortSignal.any([
    AbortSignal.timeout(AUTH_DEADLINE_MS),
    ...(options.signal ? [options.signal] : []),
  ]);
  const clientHeader = process.env.TRUSTED_CLIENT_IP_HEADER;
  const clientAddress = clientHeader ? trustedClientAddress(await headers(), clientHeader) : null;
  const response = await fetch(new URL(path, origin), {
    method: options.method ?? "GET",
    headers: {
      Accept: "application/json",
      ...(options.body ? { "Content-Type": "application/json" } : {}),
      ...(options.token ? { Authorization: `Bearer ${options.token}` } : {}),
      ...(clientAddress ? { "X-Forwarded-For": clientAddress } : {}),
    },
    body: options.body ? JSON.stringify(options.body) : undefined,
    cache: "no-store",
    redirect: "error",
    signal,
  });

  if (
    response.status === 204 ||
    response.status === 202 ||
    response.status === 401 ||
    response.status === 403 ||
    response.status === 429
  ) {
    await response.body?.cancel();
    return { status: response.status, body: null };
  }

  const mediaType = response.headers.get("content-type")?.split(";")[0].trim();

  if (mediaType !== "application/json" && mediaType !== "application/problem+json") {
    await response.body?.cancel();
    throw new Error("The authentication service returned an unreadable response.");
  }

  try {
    return { status: response.status, body: await readBoundedJson(response.body, signal) };
  } catch {
    throw new Error("The authentication service response could not be confirmed.");
  }
}

// Maps known client errors directly; marks unconfirmed upstream failures as 502 uncertain.
export function authFailure(error: unknown) {
  if (error instanceof AuthHttpError) {
    return authJson({ message: error.message }, error.status);
  }

  return authJson(
    {
      message: "The service response could not be confirmed. Retry the same request.",
      uncertain: true,
    },
    502,
  );
}

export function upstreamDenial(status: number) {
  const messages: Record<number, string> = {
    400: "Check your account details and try again.",
    401: "Email or password is incorrect, or the session has ended.",
    403: "This account cannot access the requested service.",
    409: "These details conflict with an existing account or request. Use your existing account.",
    429: "Too many attempts. Wait before trying again.",
  };

  if (!(status in messages)) {
    return authFailure(new Error("Unconfirmed upstream result"));
  }

  return authJson({ message: messages[status] }, status);
}
