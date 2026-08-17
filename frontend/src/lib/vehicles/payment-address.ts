import { isRecord } from "@/lib/auth/contracts";

const PAYMENT_HOSTS = ["billplz.com", "billplz-sandbox.com"];

// Only the payment provider's own pages are valid places to send a customer to pay.
export function providerPaymentAddress(value: unknown): string | null {
  if (!isRecord(value) || typeof value.paymentUrl !== "string") {
    return null;
  }

  try {
    const address = new URL(value.paymentUrl);
    const hostedByProvider = PAYMENT_HOSTS.some(
      (host) => address.hostname === host || address.hostname.endsWith(`.${host}`),
    );
    return address.protocol === "https:" && hostedByProvider ? address.toString() : null;
  } catch {
    return null;
  }
}
