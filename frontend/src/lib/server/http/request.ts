import "server-only";
import { HttpError } from "@/lib/http";

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
