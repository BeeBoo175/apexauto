import type { ApiRequest } from "@/lib/api";
import { privateApiFetch } from "@/lib/api-browser";
import { assistedHeaders } from "./assisted-customer";

// A private request made for the named customer; without one it is the ordinary private request.
export function privateFetchFor(customerId?: string): ApiRequest {
  if (!customerId) {
    return privateApiFetch;
  }
  return (path, parse, options = {}) =>
    privateApiFetch(path, parse, {
      ...options,
      headers: { ...options.headers, ...assistedHeaders(customerId) },
    });
}
