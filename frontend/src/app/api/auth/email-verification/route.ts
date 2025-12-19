import {
  authFailure,
  authJson,
  authUpstream,
  readAuthRequest,
  upstreamDenial,
} from "@/lib/auth/http";

export async function POST(request: Request) {
  try {
    const body = await readAuthRequest(request, "verify-email");
    const response = await authUpstream("/api/auth/email-verification", {
      method: "POST",
      body,
      signal: request.signal,
    });

    if (response.status === 204) {
      return authJson({ ok: true });
    }

    if (response.status === 400) {
      return authJson({ message: "The verification link is invalid or expired." }, 400);
    }

    return upstreamDenial(response.status);
  } catch (error) {
    return authFailure(error);
  }
}
