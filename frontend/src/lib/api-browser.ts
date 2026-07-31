import { isRecord } from "@/lib/auth/contracts";
import { parseActor } from "@/lib/auth/actor";
import { API_DEADLINE_MS, type ApiConflict, type ApiResult } from "./api";
import { parseRetryAfterSeconds, readBoundedJson } from "./http";
import { sessionChanges } from "./auth/browser-session";

// Owner decision, 8 November 2026 (dashboard pages): a dashboard read is made once and reused until the user refreshes the page
// (browser reload clears this memory) or presses a refresh control, trading some freshness for server load.
// Records, their histories and every other nested read therefore open from here after the first load. Only
// confirmed successes are kept, a refresh control or any write forgets everything, and a changed session
// forgets it too, so one account's reads are never shown to another.
const snapshots = new Map<string, ApiResult<unknown>>();
const MAXIMUM_SNAPSHOTS = 300;
// Staff dashboard reads and branch choices are reused. Exports always go to the server; public branch
// pickers opt into fresh reads, like current public availability and customer reads.
const reusablePath =
  /^(?!.*\/export(?:\?|$))\/api\/(?:staff\/|staff-accounts(?:[/?]|$)|customer-accounts(?:[/?]|$)|intake\/branches(?:\?|$))/;

export function forgetSnapshots() {
  snapshots.clear();
}

if (typeof window !== "undefined") {
  window.addEventListener("apexauto-session-invalid", forgetSnapshots);
  sessionChanges?.addEventListener("message", forgetSnapshots);
}

function remember(path: string, result: ApiResult<unknown>) {
  if (snapshots.size >= MAXIMUM_SNAPSHOTS) {
    const oldest = snapshots.keys().next();
    if (!oldest.done) {
      snapshots.delete(oldest.value);
    }
  }
  snapshots.set(path, result);
}

interface BrowserRequestPolicy {
  deadlineMs?: number;
  maximumResponseBytes?: number;
  requireActor?: boolean;
  parseConflict?: (code: unknown) => ApiConflict | undefined;
  // Reads from the server even when a snapshot exists.
  fresh?: boolean;
}

export function privateApiFetch<T>(
  path: string,
  parse: (value: unknown) => T | null,
  options: RequestInit = {},
  policy: Omit<BrowserRequestPolicy, "requireActor"> = {},
): Promise<ApiResult<T>> {
  return apiFetch(path, parse, options, { ...policy, requireActor: true });
}

export async function apiFetch<T>(
  path: string,
  parse: (value: unknown) => T | null,
  options: RequestInit = {},
  policy: BrowserRequestPolicy = {},
): Promise<ApiResult<T>> {
  const isRead = (options.method ?? "GET").toUpperCase() === "GET";
  if (!isRead) {
    // A write, confirmed or not, may have changed anything that was read before it.
    forgetSnapshots();
  }
  const reusable = isRead && reusablePath.test(path);
  if (reusable && !policy.fresh) {
    const snapshot = snapshots.get(path);
    if (snapshot) {
      return snapshot as ApiResult<T>;
    }
  }
  const result = await requestApi(path, parse, options, policy);
  if (!isRead) {
    forgetSnapshots();
  } else if (reusable && result.status === 200 && result.data) {
    remember(path, result);
  }
  return result;
}

async function requestApi<T>(
  path: string,
  parse: (value: unknown) => T | null,
  options: RequestInit,
  policy: BrowserRequestPolicy,
): Promise<ApiResult<T>> {
  const signal = AbortSignal.any([
    AbortSignal.timeout(policy.deadlineMs ?? API_DEADLINE_MS + 2000),
    ...(options.signal ? [options.signal] : []),
  ]);
  const result = await requestAttempt(path, parse, options, policy, signal);
  if (
    (options.method ?? "GET").toUpperCase() !== "GET" ||
    result.status !== 429 ||
    result.uncertain ||
    !result.retryAfterSeconds ||
    result.retryAfterSeconds > 10
  ) {
    return result;
  }
  await waitForRetry(result.retryAfterSeconds * 1000 + Math.random() * 500, signal);
  try {
    return await requestAttempt(path, parse, options, policy, signal);
  } catch {
    signal.throwIfAborted();
    return result;
  }
}

function waitForRetry(milliseconds: number, signal: AbortSignal): Promise<void> {
  signal.throwIfAborted();
  return new Promise((resolve, reject) => {
    const cancel = () => {
      clearTimeout(timer);
      reject(signal.reason);
    };
    const timer = setTimeout(() => {
      signal.removeEventListener("abort", cancel);
      resolve();
    }, milliseconds);
    signal.addEventListener("abort", cancel, { once: true });
  });
}

async function requestAttempt<T>(
  path: string,
  parse: (value: unknown) => T | null,
  options: RequestInit,
  policy: BrowserRequestPolicy,
  signal: AbortSignal,
): Promise<ApiResult<T>> {
  signal.throwIfAborted();
  const response = await fetch(path, { ...options, cache: "no-store", signal });
  if (response.status === 401) {
    window.dispatchEvent(new Event("apexauto-session-invalid"));
  }
  const value = await readBoundedJson(
    response.body,
    signal,
    policy.maximumResponseBytes ?? 1200 * 1024,
  );
  if (
    !isRecord(value) ||
    value.status !== response.status ||
    typeof value.uncertain !== "boolean" ||
    typeof value.message !== "string"
  ) {
    throw new Error("Unreadable service response");
  }
  const actor = parseActor(value.actor);
  if (actor) {
    window.dispatchEvent(new CustomEvent("apexauto-intake-actor", { detail: actor }));
  }
  const data = response.ok ? parse(value.data) : null;
  if (response.ok && (!data || (policy.requireActor && !actor))) {
    throw new Error("Unconfirmed service response");
  }
  const conflict = response.status === 409 ? policy.parseConflict?.(value.code) : undefined;
  const retryAfterSeconds =
    response.status === 429
      ? parseRetryAfterSeconds(response.headers.get("retry-after"))
      : undefined;
  return {
    status: response.status,
    data,
    message: conflict?.message ?? value.message,
    uncertain: value.uncertain,
    ...(conflict ? { code: conflict.code } : {}),
    ...(retryAfterSeconds ? { retryAfterSeconds } : {}),
  };
}
