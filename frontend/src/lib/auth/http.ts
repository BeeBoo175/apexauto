import "server-only";

import { requestApi, type ApiRequestOptions } from "@/lib/server/http/upstream";
import { HttpError, parseRetryAfterSeconds, readBoundedJson } from "@/lib/http";
import { requireSameOrigin } from "@/lib/server/http/request";
import { AUTH_BODY_LIMIT, AUTH_DEADLINE_MS, isRecord, isUuid } from "./contracts";

export function authJson(body: unknown, status = 200, retryAfterSeconds?: number) {
  return Response.json(body, {
    status,
    headers: {
      "Cache-Control": "no-store",
      ...(retryAfterSeconds ? { "Retry-After": String(retryAfterSeconds) } : {}),
    },
  });
}

// Validate payload shape, whitelist allowed fields, and require UUID idempotency keys.
export async function readAuthRequest(
  request: Request,
  kind:
    | "login"
    | "register"
    | "logout"
    | "verify-email"
    | "resend-verification"
    | "password-recovery"
    | "password-reset",
) {
  requireSameOrigin(request);

  if (
    request.headers.get("content-type")?.split(";")[0].trim().toLowerCase() !== "application/json"
  ) {
    throw new HttpError(415, "Send application/json.");
  }

  if (Number(request.headers.get("content-length")) > AUTH_BODY_LIMIT) {
    throw new HttpError(413, "The request is too large.");
  }

  const signal = AbortSignal.any([request.signal, AbortSignal.timeout(AUTH_DEADLINE_MS)]);
  let value: unknown;

  try {
    value = await readBoundedJson(request.body, signal, AUTH_BODY_LIMIT);
  } catch (error) {
    if (error instanceof HttpError) {
      throw error;
    }

    throw new HttpError(400, "The JSON request could not be read.");
  }

  if (kind === "logout") {
    if (!isRecord(value) || Object.keys(value).length !== 0) {
      throw new HttpError(400, "Sign out requires an empty JSON object.");
    }

    return {};
  }

  if (kind === "verify-email") {
    if (!isRecord(value) || typeof value.token !== "string" || value.token.length > 160) {
      throw new HttpError(400, "The verification link is invalid.");
    }

    return { token: value.token };
  }

  if (kind === "resend-verification") {
    if (!isRecord(value) || !isUuid(value.resendKey) || Object.keys(value).length !== 1) {
      throw new HttpError(400, "The resend request is invalid.");
    }

    return { resendKey: value.resendKey };
  }

  if (kind === "password-recovery") {
    if (
      !isRecord(value) ||
      typeof value.email !== "string" ||
      value.email.length > 254 ||
      Object.keys(value).length !== 1
    ) {
      throw new HttpError(400, "Enter your account email.");
    }

    return { email: value.email };
  }

  if (kind === "password-reset") {
    if (
      !isRecord(value) ||
      typeof value.token !== "string" ||
      !value.token ||
      value.token.length > 160 ||
      typeof value.password !== "string" ||
      !value.password ||
      value.password.length > 128 ||
      Object.keys(value).length !== 2
    ) {
      throw new HttpError(400, "The password reset request is invalid.");
    }

    return { token: value.token, password: value.password };
  }

  const key = kind === "login" ? "loginKey" : "registrationKey";
  const fields =
    kind === "login"
      ? ["email", "password"]
      : ["legalName", "email", "password", "nric", "mobile", "residentialAddress", "username"];

  if (
    !isRecord(value) ||
    !isUuid(value[key]) ||
    fields.some((field) => typeof value[field] !== "string")
  ) {
    throw new HttpError(400, "The authentication request is invalid.");
  }

  return Object.fromEntries([key, ...fields].map((field) => [field, value[field]]));
}

// Auth owns bodyless statuses, response validation and uncertain outcomes.
export async function authUpstream(path: string, options: ApiRequestOptions = {}) {
  const { response, signal } = await requestApi(path, AUTH_DEADLINE_MS, options);

  const retryAfterSeconds =
    response.status === 429
      ? parseRetryAfterSeconds(response.headers.get("retry-after"))
      : undefined;

  if (
    response.status === 204 ||
    response.status === 202 ||
    response.status === 401 ||
    response.status === 403 ||
    response.status === 429
  ) {
    await response.body?.cancel();
    return { status: response.status, body: null, retryAfterSeconds };
  }

  const mediaType = response.headers.get("content-type")?.split(";")[0].trim();

  if (mediaType !== "application/json" && mediaType !== "application/problem+json") {
    await response.body?.cancel();
    throw new Error("The authentication service returned an unreadable response.");
  }

  try {
    return {
      status: response.status,
      body: await readBoundedJson(response.body, signal, AUTH_BODY_LIMIT),
      retryAfterSeconds,
    };
  } catch {
    throw new Error("The authentication service response could not be confirmed.");
  }
}

// Maps known client errors directly; marks unconfirmed upstream failures as 502 uncertain.
export function authFailure(error: unknown) {
  if (error instanceof HttpError) {
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

// The backend authors the username message, so it is safe to relay to the registration form.
export function usernameDenial(body: unknown) {
  if (!isRecord(body) || !isRecord(body.errors) || !Array.isArray(body.errors.username)) {
    return null;
  }

  const message: unknown = body.errors.username[0];
  return typeof message === "string" ? authJson({ message }, 400) : null;
}

export function upstreamDenial(status: number, retryAfterSeconds?: number) {
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

  const seconds =
    status === 429 && retryAfterSeconds !== undefined
      ? parseRetryAfterSeconds(String(retryAfterSeconds))
      : undefined;
  const message = seconds
    ? `Too many attempts. Wait ${seconds} seconds before trying again.`
    : messages[status];
  return authJson({ message }, status, seconds);
}
