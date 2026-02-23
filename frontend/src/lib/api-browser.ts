import { isRecord } from "@/lib/auth/contracts";
import { parseActor } from "@/lib/auth/actor";
import { API_DEADLINE_MS, type ApiResult } from "./api";
import { readBoundedJson } from "./http";

export async function apiFetch<T>(
  path: string,
  parse: (value: unknown) => T | null,
  options: RequestInit = {},
  policy: {
    deadlineMs?: number;
    maximumResponseBytes?: number;
    requireActor?: boolean;
  } = {},
): Promise<ApiResult<T>> {
  const signal = AbortSignal.any([
    AbortSignal.timeout(policy.deadlineMs ?? API_DEADLINE_MS + 2000),
    ...(options.signal ? [options.signal] : []),
  ]);
  const response = await fetch(path, { ...options, cache: "no-store", signal });
  if (response.status === 401 || response.status === 403) {
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
  const data = parse(value.data);
  if (response.ok && (!data || (policy.requireActor && !actor))) {
    throw new Error("Unconfirmed service response");
  }
  return { status: response.status, data, message: value.message, uncertain: value.uncertain };
}
