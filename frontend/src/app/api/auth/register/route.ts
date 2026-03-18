import {
  authFailure,
  authJson,
  authUpstream,
  readAuthRequest,
  upstreamDenial,
} from "@/lib/auth/http";

export async function POST(request: Request) {
  try {
    const body = await readAuthRequest(request, "register");
    const response = await authUpstream("/api/auth/register", {
      method: "POST",
      body,
      signal: request.signal,
    });

    if (response.status !== 204) {
      return upstreamDenial(response.status, response.retryAfterSeconds);
    }

    return authJson({ ok: true });
  } catch (error) {
    return authFailure(error);
  }
}
