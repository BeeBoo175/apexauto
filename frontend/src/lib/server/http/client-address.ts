import { isIP } from "node:net";

export function trustedClientAddress(
  headers: Pick<Headers, "get">,
  headerName?: string,
): string | null {
  if (!headerName) {
    return null;
  }

  if (!/^[a-zA-Z0-9-]+$/.test(headerName)) {
    throw new Error("TRUSTED_CLIENT_IP_HEADER must be a valid header name.");
  }

  const address = headers.get(headerName)?.trim();

  // The ingress must overwrite this field with one socket-derived address, never a chain.
  if (!address || !isIP(address)) {
    throw new Error("The trusted ingress client address is missing or invalid.");
  }

  return address;
}
