import "server-only";
import { readBoundedJson } from "@/lib/http";
import { createProbeBudget } from "./budget";
import { buildReport, type ApiReply, type ReadinessReport } from "./contracts";

// A sleeping Render service can take about a minute to start, so one call never waits for it: it answers
// waking after this deadline and the caller asks again.
const API_DEADLINE_MS = 6_000;
const API_REPLY_MAXIMUM_BYTES = 1_024;

export const probeBudget = createProbeBudget(30, 60_000);

async function readApi(): Promise<ApiReply | null> {
  const origin = process.env.API_BASE_URL;
  if (!origin) {
    return null;
  }
  const signal = AbortSignal.timeout(API_DEADLINE_MS);
  try {
    // The health route is exempt from business rate limits and carries no visitor identity, so this is a plain
    // read rather than the authenticated transport.
    const response = await fetch(new URL("/health/ready", origin), {
      headers: { Accept: "application/json" },
      cache: "no-store",
      redirect: "error",
      signal,
    });
    const body = await readBoundedJson(response.body, signal, API_REPLY_MAXIMUM_BYTES).catch(
      () => null,
    );
    return { status: response.status, body };
  } catch {
    return null;
  }
}

let inFlight: Promise<ReadinessReport> | null = null;

// Callers that arrive while a probe is running share it, so a burst of visitors makes one upstream request. The
// result is never kept afterwards: the next call probes again.
export function probeReadiness() {
  inFlight ??= readApi()
    .then((reply) => buildReport(reply, new Date()))
    .finally(() => {
      inFlight = null;
    });
  return inFlight;
}
