import {
  authFailure,
  authJson,
  authUpstream,
  readAuthRequest,
  upstreamDenial,
} from "@/lib/auth/http";
import { isRecord } from "@/lib/auth/contracts";
import { staffActivationConflict } from "@/lib/auth/staff-invitation";

export async function POST(request: Request) {
  try {
    const body = await readAuthRequest(request, "staff-invitation-activation");
    const response = await authUpstream("/api/auth/staff-invitation/activation", {
      method: "POST",
      body,
      signal: request.signal,
    });

    if (response.status === 204) {
      return authJson({ ok: true });
    }

    if (response.status === 400) {
      return authJson(
        {
          message:
            "Check the password and username. Passwords need 8 or more characters with uppercase, lowercase, a digit and a symbol; usernames use 3 to 30 letters, numbers, dots or underscores.",
        },
        400,
      );
    }

    if (response.status === 409) {
      const code = isRecord(response.body) ? response.body.code : null;
      return authJson({ message: staffActivationConflict(code) }, 409);
    }

    return upstreamDenial(response.status, response.retryAfterSeconds);
  } catch (error) {
    return authFailure(error);
  }
}
