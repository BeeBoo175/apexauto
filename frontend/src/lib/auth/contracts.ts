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

// Pin return targets strictly to /account to prevent open-redirect phishing.
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

  if (destination.origin !== "https://local.invalid" || destination.pathname !== "/account") {
    return "/account";
  }

  return `${destination.pathname}${destination.search}${destination.hash}`;
}
