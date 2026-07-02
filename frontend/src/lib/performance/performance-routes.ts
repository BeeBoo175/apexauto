import "server-only";
import { API_DEADLINE_MS } from "@/lib/api";
import { apiError, apiJson, privateApiRead } from "@/lib/server/http/api";
import { accessDenied, allowedQuery, confirmedActor } from "@/lib/server/http/staff-relay";
import { HttpError } from "@/lib/http";
import {
  hasPerformanceAccess,
  sectionNames,
  sectionParsers,
  type SectionName,
} from "./performance";

const queryKeys = ["from", "to", "branchId"];

function isSection(value: string): value is SectionName {
  return (sectionNames as readonly string[]).includes(value);
}

// Each section is its own bounded read so one slow or failed section never holds the others back.
export async function relayPerformanceSection(request: Request, section: string) {
  try {
    if (!isSection(section)) {
      throw new HttpError(404, "Choose a performance section.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const actor = await confirmedActor(signal);
    const allowed =
      hasPerformanceAccess(actor) && (section !== "branches" || actor.role === "GeneralManager");
    if (!allowed) {
      return accessDenied(actor, "Manager access required.");
    }
    const query = allowedQuery(new URL(request.url).searchParams, queryKeys);
    const result = await privateApiRead(
      `/api/performance/${section}${query.size > 0 ? `?${query}` : ""}`,
      sectionParsers[section] as (value: unknown) => unknown,
      signal,
    );
    return apiJson({ ...result, actor: (result.data as { actor?: typeof actor })?.actor ?? actor });
  } catch (error) {
    return apiError(error);
  }
}
