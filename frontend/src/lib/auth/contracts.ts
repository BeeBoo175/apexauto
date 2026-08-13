export const SESSION_COOKIE = "apexauto_token";
export const AUTH_BODY_LIMIT = 16 * 1024;
export const AUTH_DEADLINE_MS = 10_000;

export interface CustomerAccount {
  userId: string;
  legalName: string;
  email: string;
  emailVerified: boolean;
}

export function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

export function isUuid(value: unknown): value is string {
  return (
    typeof value === "string" &&
    /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value) &&
    value !== "00000000-0000-0000-0000-000000000000"
  );
}

export function isNonNegativeInteger(value: unknown): value is number {
  return Number.isSafeInteger(value) && Number(value) >= 0;
}

export function isPositiveInteger(value: unknown): value is number {
  return Number.isSafeInteger(value) && Number(value) > 0;
}

// A UTC instant as the backend serializes it; offsets other than UTC are rejected.
export function isUtcInstant(value: unknown): value is string {
  return (
    typeof value === "string" && /(?:Z|\+00:00)$/.test(value) && Number.isFinite(Date.parse(value))
  );
}

export function parseAccount(value: unknown): CustomerAccount | null {
  if (
    !isRecord(value) ||
    !isUuid(value.userId) ||
    typeof value.legalName !== "string" ||
    !value.legalName.trim() ||
    typeof value.email !== "string" ||
    !value.email.trim() ||
    typeof value.emailVerified !== "boolean"
  ) {
    return null;
  }

  return {
    userId: value.userId,
    legalName: value.legalName,
    email: value.email,
    emailVerified: value.emailVerified,
  };
}

// Enforce valid 3-part JWT and <3.8KB size to fit inside browser cookie limits.
export function parseSession(value: unknown) {
  if (
    !isRecord(value) ||
    typeof value.accessToken !== "string" ||
    !/^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$/.test(value.accessToken) ||
    value.accessToken.length > 3800 ||
    typeof value.expiresAt !== "string" ||
    !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|\+00:00)$/.test(value.expiresAt)
  ) {
    return null;
  }

  const expiresAt = new Date(value.expiresAt);

  if (!Number.isFinite(expiresAt.getTime()) || expiresAt.getTime() <= Date.now()) {
    return null;
  }

  return { accessToken: value.accessToken, expiresAt };
}

// Only implemented task destinations may receive an auth return.
export function safeReturnTo(value: unknown): string {
  if (
    typeof value !== "string" ||
    !value.startsWith("/") ||
    value.startsWith("//") ||
    /[\\\s\x00-\x1f\x7f]/.test(value)
  ) {
    return "/account";
  }

  const destination = new URL(value, "https://local.invalid");

  if (
    destination.origin !== "https://local.invalid" ||
    !(
      ["/account", "/sell", "/dashboard", "/dashboard/intake"].includes(destination.pathname) ||
      (destination.pathname.startsWith("/vehicles/") &&
        isUuid(destination.pathname.slice("/vehicles/".length))) ||
      /^\/dashboard\/intake\/[0-9a-f-]{36}$/i.test(destination.pathname)
    )
  ) {
    return "/account";
  }

  return `${destination.pathname}${destination.search}${destination.hash}`;
}

export function authHref(path: "/login" | "/auth/continue", returnTo?: unknown): string {
  const destination = safeReturnTo(returnTo);
  if (destination === "/account") {
    return path;
  }
  return `${path}?returnTo=${encodeURIComponent(destination)}`;
}

// The customer account a staff read names, or null where the read does not carry it.
export function customerIdOf(value: Record<string, unknown>) {
  return isUuid(value.customerId) ? value.customerId : null;
}
