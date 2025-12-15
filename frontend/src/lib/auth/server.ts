import "server-only";

import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { SESSION_COOKIE, parseAccount } from "./contracts";
import { authUpstream } from "./http";

// HttpOnly hides the token from JavaScript; SameSite=Lax limits cross-site cookie sends.
// Mutating routes still require an explicit Origin check; production cookies require HTTPS.
const cookieOptions = {
  httpOnly: true,
  sameSite: "lax" as const,
  secure: process.env.NODE_ENV === "production",
  path: "/",
};

// BFF token encapsulation: converts upstream JWT into an HttpOnly session cookie
// and returns `{ ok: true }` so raw bearer tokens are never exposed to browser JavaScript.
export function sessionResponse(session: { accessToken: string; expiresAt: Date }) {
  const response = NextResponse.json({ ok: true }, { headers: { "Cache-Control": "no-store" } });
  response.cookies.set(SESSION_COOKIE, session.accessToken, {
    ...cookieOptions,
    expires: session.expiresAt,
  });
  return response;
}

// Invalidate session cookie by setting maxAge to 0, wiping browser-persisted credentials.
export function expiredSessionResponse() {
  const response = NextResponse.json({ ok: true }, { headers: { "Cache-Control": "no-store" } });
  response.cookies.set(SESSION_COOKIE, "", { ...cookieOptions, maxAge: 0 });
  return response;
}

// BFF credential relay: extracts session cookie within Next.js server context
// and forwards it as a Bearer token to fetch the authenticated user profile.
export async function readOwnAccount(signal?: AbortSignal) {
  const token = (await cookies()).get(SESSION_COOKIE)?.value;

  if (!token) {
    return { status: 401, account: null };
  }

  const response = await authUpstream("/api/account", { token, signal });

  if (response.status !== 200) {
    return { status: response.status, account: null };
  }

  const account = parseAccount(response.body);

  if (!account) {
    throw new Error("The account response is invalid.");
  }

  return { status: 200, account };
}
