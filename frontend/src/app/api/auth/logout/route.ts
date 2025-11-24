import { cookies } from "next/headers";
import { SESSION_COOKIE } from "@/lib/auth/contracts";
import { authFailure, authUpstream, readAuthRequest, upstreamDenial } from "@/lib/auth/http";
import { expiredSessionResponse } from "@/lib/auth/server";

export async function POST(request: Request) {
  try {
    await readAuthRequest(request, "logout");
    const token = (await cookies()).get(SESSION_COOKIE)?.value;

    if (!token) {
      return expiredSessionResponse();
    }

    const response = await authUpstream("/api/auth/logout", {
      method: "POST",
      token,
      signal: request.signal,
    });

    // A rejected session is already unusable; an unavailable service cannot confirm revocation.
    if (response.status === 204 || response.status === 401) {
      return expiredSessionResponse();
    }

    return upstreamDenial(response.status);
  } catch (error) {
    return authFailure(error);
  }
}
