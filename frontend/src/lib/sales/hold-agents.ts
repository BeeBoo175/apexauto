import { isRecord, isUuid } from "@/lib/auth/contracts";

// A sales agent of the hold's branch that a manager can give the hold to; staff appear by email.
export interface HoldAgent {
  id: string;
  email: string;
}

export function parseHoldAgents(value: unknown): HoldAgent[] | null {
  if (!Array.isArray(value)) {
    return null;
  }
  const agents: HoldAgent[] = [];
  for (const item of value) {
    if (!isRecord(item) || !isUuid(item.id) || typeof item.email !== "string" || !item.email) {
      return null;
    }
    agents.push({ id: item.id, email: item.email });
  }
  return new Set(agents.map((agent) => agent.id)).size === agents.length ? agents : null;
}
