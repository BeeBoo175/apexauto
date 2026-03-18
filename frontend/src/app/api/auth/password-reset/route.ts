import { isRecord } from "@/lib/auth/contracts";
import {
  authFailure,
  authJson,
  authUpstream,
  readAuthRequest,
  upstreamDenial,
} from "@/lib/auth/http";

export async function POST(request: Request) {
  try {
    const body = await readAuthRequest(request, "password-reset");
    const response = await authUpstream("/api/auth/password-reset", {
      method: "POST",
      body,
      signal: request.signal,
    });

    if (response.status === 204) {
      return authJson({ ok: true });
    }

    if (response.status !== 400) {
      return upstreamDenial(response.status, response.retryAfterSeconds);
    }

    const problem = response.body;

    if (isRecord(problem) && isRecord(problem.errors)) {
      const errors = problem.errors.password;

      if (
        Array.isArray(errors) &&
        errors.length > 0 &&
        errors.length <= 10 &&
        errors.every(
          (error) => typeof error === "string" && error.length > 0 && error.length <= 512,
        )
      ) {
        return authJson(
          {
            message: "Choose a password that meets account policy.",
            passwordError: errors.join(" "),
          },
          400,
        );
      }
    }

    if (isRecord(problem) && problem.title === "Reset link is invalid or expired.") {
      return authJson(
        { message: "The reset link is invalid or expired. Request a new link." },
        400,
      );
    }

    return authFailure(new Error("Unconfirmed password reset denial"));
  } catch (error) {
    return authFailure(error);
  }
}
