import "server-only";

import { headers } from "next/headers";
import { trustedClientAddress } from "./client-address";

export interface ApiRequestOptions {
  body?: unknown;
  token?: string;
  signal?: AbortSignal;
  method?: string;
  bytes?: Uint8Array;
  contentType?: string;
  expectedActor?: string;
}

export async function requestApi(
  path: string,
  deadlineMs: number,
  options: ApiRequestOptions = {},
) {
  const origin = process.env.API_BASE_URL;

  if (!origin) {
    throw new Error("API_BASE_URL is required.");
  }

  const signal = AbortSignal.any([
    AbortSignal.timeout(deadlineMs),
    ...(options.signal ? [options.signal] : []),
  ]);
  const clientHeader = process.env.TRUSTED_CLIENT_IP_HEADER;
  const clientAddress = clientHeader ? trustedClientAddress(await headers(), clientHeader) : null;
  let body: BodyInit | undefined;
  if (options.body) body = JSON.stringify(options.body);
  if (options.bytes) body = Buffer.from(options.bytes);
  const response = await fetch(new URL(path, origin), {
    method: options.method ?? "GET",
    headers: {
      Accept: "application/json",
      ...(options.body ? { "Content-Type": "application/json" } : {}),
      ...(options.bytes
        ? { "Content-Type": options.contentType ?? "application/octet-stream" }
        : {}),
      ...(options.token ? { Authorization: `Bearer ${options.token}` } : {}),
      ...(options.expectedActor ? { "X-Expected-Actor": options.expectedActor } : {}),
      ...(clientAddress ? { "X-Forwarded-For": clientAddress } : {}),
    },
    body,
    cache: "no-store",
    redirect: "error",
    signal,
  });

  // Keep the same deadline active while the feature consumes the response body.
  return { response, signal };
}
