import { isRecord } from "@/lib/auth/contracts";
import { INTAKE_DEADLINE_MS } from "./contracts";
import type { ApiResult } from "./contracts";
import { parseActor } from "@/lib/auth/actor";

export async function intakeFetch<T>(
  path: string,
  parse: (value: unknown) => T | null,
  options: RequestInit = {},
  deadline = INTAKE_DEADLINE_MS,
): Promise<ApiResult<T>> {
  const signal = AbortSignal.any([
    AbortSignal.timeout(deadline + 2000),
    ...(options.signal ? [options.signal] : []),
  ]);
  const response = await fetch(path, { ...options, cache: "no-store", signal });
  if (response.status === 401 || response.status === 403) {
    window.dispatchEvent(new Event("apexauto-session-invalid"));
  }
  const value: unknown = await response.json();
  if (
    !isRecord(value) ||
    value.status !== response.status ||
    typeof value.uncertain !== "boolean" ||
    typeof value.message !== "string"
  ) {
    throw new Error("Unreadable service response");
  }
  const data = parse(value.data);
  const actor = parseActor(value.actor);
  if (actor) {
    window.dispatchEvent(new CustomEvent("apexauto-intake-actor", { detail: actor }));
  }
  if (response.ok && !data) {
    throw new Error("Unconfirmed service response");
  }
  return { status: response.status, data, message: value.message, uncertain: value.uncertain };
}
