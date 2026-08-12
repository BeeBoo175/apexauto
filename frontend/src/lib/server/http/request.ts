import "server-only";
import { isUuid } from "@/lib/auth/contracts";
import { HttpError } from "@/lib/http";
import { ON_BEHALF_OF_HEADER } from "@/lib/staff/assisted-customer";

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
    throw new HttpError(403, "This action requires a same-origin request.");
  }
}

// The customer a staff member names to act for, or undefined when the caller acts for themselves.
export function assistedCustomerId(request: Request) {
  const value = request.headers.get(ON_BEHALF_OF_HEADER);
  if (value === null) {
    return undefined;
  }
  if (!isUuid(value)) {
    throw new HttpError(400, "Choose a valid customer to act for.");
  }
  return value;
}
