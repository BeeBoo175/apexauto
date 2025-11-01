import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

export function proxy(request: NextRequest) {
  const token = request.cookies.get("apexauto_access_token")?.value;
  const { pathname, search } = request.nextUrl;

  // Intercept protected namespaces at the edge
  if (!token) {
    const loginUrl = new URL("/login", request.url);
    loginUrl.searchParams.set("returnTo", `${pathname}${search}`);
    return NextResponse.redirect(loginUrl);
  }

  const response = NextResponse.next();

  // Fortify protected routes with anti-indexing and security headers
  response.headers.set("X-Robots-Tag", "noindex, nofollow, noarchive, nosnippet");
  response.headers.set("X-Frame-Options", "DENY");
  response.headers.set("X-Content-Type-Options", "nosniff");

  return response;
}

export const config = {
  matcher: [
    "/dashboard/:path*",
    "/account/:path*",
    "/documents/:path*",
  ],
};

export default proxy;

