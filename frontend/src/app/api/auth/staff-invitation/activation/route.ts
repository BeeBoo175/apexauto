import {
  authFailure,
  authJson,
  authUpstream,
  readAuthRequest,
  upstreamDenial,
} from "@/lib/auth/http";
import { isRecord } from "@/lib/auth/contracts";

const conflictMessages: Record<string, string> = {
  UsernameTaken: "That username is taken. Choose another.",
  EmailTaken: "An account already uses this email. Ask an administrator for help.",
  Unavailable: "This invitation link is no longer available. Ask an administrator for a new one.",
};

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
      const message = typeof code === "string" ? conflictMessages[code] : undefined;
      return authJson({ message: message ?? conflictMessages.Unavailable }, 409);
    }

    return upstreamDenial(response.status, response.retryAfterSeconds);
  } catch (error) {
    return authFailure(error);
  }
}
