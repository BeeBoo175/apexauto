import {
  authFailure,
  authJson,
  authUpstream,
  readAuthRequest,
  upstreamDenial,
} from "@/lib/auth/http";

export async function POST(request: Request) {
  try {
    const body = await readAuthRequest(request, "password-recovery");
    const response = await authUpstream("/api/auth/password-recovery", {
      method: "POST",
      body,
      signal: request.signal,
    });

    if (response.status === 202) {
      return authJson({ ok: true });
    }

    return upstreamDenial(response.status);
  } catch (error) {
    return authFailure(error);
  }
}
