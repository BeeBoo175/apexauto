import {
  authFailure,
  authJson,
  authUpstream,
  readAuthRequest,
  upstreamDenial,
} from "@/lib/auth/http";

export async function POST(request: Request) {
  try {
    const body = await readAuthRequest(request, "reservation-deposit");
    const response = await authUpstream("/api/reservation-deposits/confirmations", {
      method: "POST",
      body,
      signal: request.signal,
    });

    if (response.status === 204) {
      return authJson({ ok: true });
    }

    if (response.status === 400) {
      return authJson({ message: "The deposit link is invalid." }, 400);
    }

    if (response.status === 410) {
      return authJson(
        {
          message:
            "The deposit link has expired and the hold was released. Ask our team to send a new one.",
        },
        410,
      );
    }

    if (response.status === 409) {
      return authJson(
        { message: "The deposit can no longer be confirmed for this car. Contact our team." },
        409,
      );
    }

    return upstreamDenial(response.status, response.retryAfterSeconds);
  } catch (error) {
    return authFailure(error);
  }
}
