import { isRecord } from "@/lib/auth/contracts";
import { parseActor } from "@/lib/auth/actor";
import type { ApiResult } from "@/lib/intake/contracts";

async function boundedJson(response: Response, signal: AbortSignal) {
  const reader = response.body?.getReader();
  if (!reader) {
    throw new Error("Missing service response");
  }
  const cancel = () => {
    void reader.cancel().catch(() => {});
  };
  signal.addEventListener("abort", cancel, { once: true });
  const chunks: Uint8Array[] = [];
  let size = 0;
  try {
    signal.throwIfAborted();
    while (true) {
      const chunk = await reader.read();
      signal.throwIfAborted();
      if (chunk.done) {
        break;
      }
      size += chunk.value.byteLength;
      if (size > 1200 * 1024) {
        throw new Error("Service response too large");
      }
      chunks.push(chunk.value);
    }
    const bytes = new Uint8Array(size);
    let offset = 0;
    for (const chunk of chunks) {
      bytes.set(chunk, offset);
      offset += chunk.byteLength;
    }
    return JSON.parse(new TextDecoder().decode(bytes)) as unknown;
  } finally {
    await reader.cancel().catch(() => {});
    reader.releaseLock();
    signal.removeEventListener("abort", cancel);
  }
}

export async function inspectionFetch<T>(
  path: string,
  parse: (v: unknown) => T | null,
  options: RequestInit = {},
  deadline = 22_000,
): Promise<ApiResult<T>> {
  const signal = AbortSignal.any([
    AbortSignal.timeout(deadline),
    ...(options.signal ? [options.signal] : []),
  ]);
  const response = await fetch(`/api/staff/inspection/${path}`, {
    ...options,
    cache: "no-store",
    signal,
  });
  if (response.status === 401 || response.status === 403) {
    window.dispatchEvent(new Event("apexauto-session-invalid"));
  }
  const value = await boundedJson(response, signal);
  if (
    !isRecord(value) ||
    value.status !== response.status ||
    typeof value.uncertain !== "boolean" ||
    typeof value.message !== "string"
  ) {
    throw new Error("Unreadable inspection response");
  }
  const actor = parseActor(value.actor);
  if (actor) {
    window.dispatchEvent(new CustomEvent("apexauto-intake-actor", { detail: actor }));
  }
  const data = parse(value.data);
  if (response.ok && (!data || !actor)) {
    throw new Error("Unconfirmed inspection response");
  }
  return { status: response.status, data, message: value.message, uncertain: value.uncertain };
}
