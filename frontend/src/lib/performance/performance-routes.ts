import "server-only";
import { API_DEADLINE_MS } from "@/lib/api";
import { apiError, apiJson, privateApiRead } from "@/lib/server/http/api";
import { accessDenied, allowedQuery, confirmedActor } from "@/lib/server/http/staff-relay";
import { HttpError } from "@/lib/http";
import { isUuid } from "@/lib/auth/contracts";
import { parseMilestonePage, parseSalesTeam, parseWorkshopTeam } from "./team";
import {
  hasPerformanceAccess,
  sectionNames,
  sectionParsers,
  type SectionName,
} from "./performance";

const queryKeys = ["from", "to", "branchId"];
// Who has room pages its read by team, search and page.
const sectionKeys = [...queryKeys, "team", "search", "page", "pageSize"];

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
    const query = allowedQuery(new URL(request.url).searchParams, sectionKeys);
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

const teamKeys = [
  ...queryKeys,
  "page",
  "pageSize",
  "search",
  "name",
  "status",
  "sort",
  "direction",
  "min",
];

// Team output pages: one tab per read, 25 people per page, in fixed alphabetical order.
export async function relayPerformanceTeam(request: Request, tab: string) {
  try {
    if (tab !== "sales" && tab !== "workshop") {
      throw new HttpError(404, "Choose a team output tab.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const actor = await confirmedActor(signal);
    if (!hasPerformanceAccess(actor)) {
      return accessDenied(actor, "Manager access required.");
    }
    const query = allowedQuery(new URL(request.url).searchParams, teamKeys);
    const result = await privateApiRead(
      `/api/performance/team/${tab}${query.size > 0 ? `?${query}` : ""}`,
      (tab === "sales" ? parseSalesTeam : parseWorkshopTeam) as (value: unknown) => unknown,
      signal,
    );
    return apiJson({ ...result, actor: (result.data as { actor?: typeof actor })?.actor ?? actor });
  } catch (error) {
    return apiError(error);
  }
}

// One employee's milestones for the drawer and the milestone export.
export async function relayPerformanceMilestones(request: Request, id: string) {
  try {
    if (!isUuid(id)) {
      throw new HttpError(400, "Choose a valid person.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const actor = await confirmedActor(signal);
    if (!hasPerformanceAccess(actor)) {
      return accessDenied(actor, "Manager access required.");
    }
    const query = allowedQuery(new URL(request.url).searchParams, teamKeys);
    const result = await privateApiRead(
      `/api/performance/people/${id}/milestones${query.size > 0 ? `?${query}` : ""}`,
      (value) => {
        const milestones = parseMilestonePage(value);
        return milestones?.personId === id ? milestones : null;
      },
      signal,
    );
    return apiJson({ ...result, actor: result.data?.actor ?? actor });
  } catch (error) {
    return apiError(error);
  }
}
