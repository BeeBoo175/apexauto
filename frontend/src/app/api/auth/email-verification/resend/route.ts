import { cookies } from "next/headers";
import { SESSION_COOKIE } from "@/lib/auth/contracts";
import {
  authFailure,
  authJson,
  authUpstream,
  readAuthRequest,
  upstreamDenial,
} from "@/lib/auth/http";

export async function POST(request: Request) {
  try {
    const body = await readAuthRequest(request, "resend-verification");
    const token = (await cookies()).get(SESSION_COOKIE)?.value;

    if (!token) {
      return upstreamDenial(401);
    }

    const response = await authUpstream("/api/auth/email-verification/resend", {
      method: "POST",
      body,
      token,
      signal: request.signal,
    });

    if (response.status !== 202) {
      return upstreamDenial(response.status);
    }

    return authJson({ ok: true });
  } catch (error) {
    return authFailure(error);
  }
}
