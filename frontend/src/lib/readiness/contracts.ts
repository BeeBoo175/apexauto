// The public readiness report: which part of the demo answers, and how far a visitor can get. Pure rules, so they
// are tested without a server.

export type PartState = "ready" | "waking" | "down" | "unknown" | "not-checked";

export interface ReadinessParts {
  // The Vercel site: if this report was produced, it answers.
  site: PartState;
  // The Render API.
  api: PartState;
  // The Neon database, reached through the API.
  database: PartState;
  // Object storage is never read here: every check would be a billed request.
  storage: PartState;
}

export type OverallState = "ready" | "waking" | "unavailable";

export interface ReadinessReport {
  status: OverallState;
  checkedAt: string;
  parts: ReadinessParts;
}

export interface ApiReply {
  status: number;
  body: unknown;
}

function bodyStatus(body: unknown) {
  if (typeof body !== "object" || body === null || !("status" in body)) {
    return null;
  }
  return typeof body.status === "string" ? body.status : null;
}

// The API's own answers are explicit: ready, or a 503 that says the database cannot be reached. Anything else, or
// no answer inside the deadline, is a host that is still starting, which looks the same as an outage from here.
export function classifyApiReply(reply: ApiReply | null): Pick<ReadinessParts, "api" | "database"> {
  if (reply?.status === 200 && bodyStatus(reply.body) === "ready") {
    return { api: "ready", database: "ready" };
  }
  if (reply?.status === 503 && bodyStatus(reply.body) === "unavailable") {
    return { api: "ready", database: "down" };
  }
  return { api: "waking", database: "unknown" };
}

// Ready only when every part that is checked is ready; a part that is down outranks one that is still waking.
export function overallState(parts: ReadinessParts): OverallState {
  const states = Object.values(parts).filter((state) => state !== "not-checked");
  if (states.includes("down")) {
    return "unavailable";
  }
  return states.every((state) => state === "ready") ? "ready" : "waking";
}

export function buildReport(reply: ApiReply | null, now: Date): ReadinessReport {
  const parts: ReadinessParts = {
    site: "ready",
    ...classifyApiReply(reply),
    storage: "not-checked",
  };
  return { status: overallState(parts), checkedAt: now.toISOString(), parts };
}
