import { isRecord } from "@/lib/auth/contracts";

// The hosts of the configured payment provider's own pages; changing provider means changing this one setting.
// NEXT_PUBLIC_ so the browser-side readers of the API see the same list as the route handlers.
const DEFAULT_PAYMENT_HOSTS = "billplz.com,billplz-sandbox.com";

function paymentHosts() {
  return (process.env.NEXT_PUBLIC_PAYMENT_HOSTS ?? DEFAULT_PAYMENT_HOSTS)
    .split(",")
    .map((host) => host.trim().toLowerCase())
    .filter(Boolean);
}

// Only the payment provider's own pages are valid places to send a customer to pay.
export function providerPaymentAddress(value: unknown): string | null {
  if (!isRecord(value) || typeof value.paymentUrl !== "string") {
    return null;
  }

  try {
    const address = new URL(value.paymentUrl);
    const hostedByProvider = paymentHosts().some(
      (host) => address.hostname === host || address.hostname.endsWith(`.${host}`),
    );
    return address.protocol === "https:" && hostedByProvider ? address.toString() : null;
  } catch {
    return null;
  }
}
