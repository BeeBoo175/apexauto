import {
  authFailure,
  authJson,
  authUpstream,
  readAuthRequest,
  upstreamDenial,
} from "@/lib/auth/http";
import { isRecord } from "@/lib/auth/contracts";

const PAYMENT_HOSTS = ["billplz.com", "billplz-sandbox.com"];

// Only the payment provider's own pages are valid places to send a customer to pay.
function paymentAddress(body: unknown) {
  if (!isRecord(body) || typeof body.paymentUrl !== "string") {
    return null;
  }

  try {
    const address = new URL(body.paymentUrl);
    const hostedByProvider = PAYMENT_HOSTS.some(
      (host) => address.hostname === host || address.hostname.endsWith(`.${host}`),
    );
    return address.protocol === "https:" && hostedByProvider ? address.toString() : null;
  } catch {
    return null;
  }
}

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

    if (response.status === 200) {
      const paymentUrl = paymentAddress(response.body);
      return paymentUrl
        ? authJson({ paymentUrl })
        : authJson({ message: "The payment page could not be opened. Try the link again." }, 502);
    }

    if (response.status === 202) {
      return authJson({ waiting: true }, 202);
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
