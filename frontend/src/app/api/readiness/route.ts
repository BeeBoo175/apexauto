import { allowedOrigin } from "@/lib/readiness/cors";
import { probeBudget, probeReadiness } from "@/lib/readiness/probe";

// The public wake-up and status check for the portfolio. It reads no cookie and returns no identity.
function responseHeaders(request: Request) {
  const origin = allowedOrigin(
    request.headers.get("origin"),
    process.env.READINESS_ALLOWED_ORIGINS,
  );
  return {
    "Cache-Control": "no-store",
    "X-Content-Type-Options": "nosniff",
    "X-Robots-Tag": "noindex, nofollow",
    Vary: "Origin",
    ...(origin ? { "Access-Control-Allow-Origin": origin } : {}),
  };
}

export async function GET(request: Request) {
  const headers = responseHeaders(request);
  const admission = probeBudget(Date.now());
  if (!admission.admitted) {
    return Response.json(
      { status: "limited", retryAfterSeconds: admission.retryAfterSeconds },
      {
        status: 429,
        headers: { ...headers, "Retry-After": String(admission.retryAfterSeconds) },
      },
    );
  }

  const report = await probeReadiness();
  return Response.json(report, { status: report.status === "ready" ? 200 : 503, headers });
}

export function OPTIONS(request: Request) {
  return new Response(null, {
    status: 204,
    headers: {
      ...responseHeaders(request),
      "Access-Control-Allow-Methods": "GET",
      "Access-Control-Max-Age": "600",
    },
  });
}
