import { parseSession } from "@/lib/auth/contracts";
import { authFailure, authUpstream, readAuthRequest, upstreamDenial } from "@/lib/auth/http";
import { sessionResponse } from "@/lib/auth/server";
export async function POST(request: Request) {
  try {
    const body = await readAuthRequest(request, "login");
    const response = await authUpstream("/api/auth/login", {
      method: "POST",
      body,
      signal: request.signal,
    });

    if (response.status !== 200) {
      return upstreamDenial(response.status, response.retryAfterSeconds);
    }

    const session = parseSession(response.body);

    if (!session) {
      throw new Error("Invalid session response");
    }

    return sessionResponse(session);
  } catch (error) {
    return authFailure(error);
  }
}
