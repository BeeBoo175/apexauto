import "server-only";
import { apiError } from "@/lib/server/http/api";
import { relayGatedRead } from "@/lib/server/http/staff-relay";
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
const sectionKeys = [...queryKeys, "view", "search", "page", "pageSize"];

const managerDenial = "Manager access required.";

function isSection(value: string): value is SectionName {
  return (sectionNames as readonly string[]).includes(value);
}

// Each section is its own bounded read so one slow or failed section never holds the others back.
export function relayPerformanceSection(request: Request, section: string) {
  if (!isSection(section)) {
    return Promise.resolve(apiError(new HttpError(404, "Choose a performance section.")));
  }

  return relayGatedRead(request, {
    dataFirst: true,
    allowed: (actor) =>
      hasPerformanceAccess(actor) && (section !== "branches" || actor.role === "GeneralManager"),
    denial: managerDenial,
    path: `/api/performance/${section}`,
    keys: sectionKeys,
    parse: sectionParsers[section],
  });
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
export function relayPerformanceTeam(request: Request, tab: string) {
  if (tab !== "sales" && tab !== "workshop") {
    return Promise.resolve(apiError(new HttpError(404, "Choose a team output tab.")));
  }

  return relayGatedRead(request, {
    dataFirst: true,
    allowed: hasPerformanceAccess,
    denial: managerDenial,
    path: `/api/performance/team/${tab}`,
    keys: teamKeys,
    parse: tab === "sales" ? parseSalesTeam : parseWorkshopTeam,
  });
}

// One employee's milestones for the drawer and the milestone export.
export function relayPerformanceMilestones(request: Request, id: string) {
  if (!isUuid(id)) {
    return Promise.resolve(apiError(new HttpError(400, "Choose a valid person.")));
  }

  return relayGatedRead(request, {
    dataFirst: true,
    allowed: hasPerformanceAccess,
    denial: managerDenial,
    path: `/api/performance/people/${id}/milestones`,
    keys: teamKeys,
    parse: (value) => {
      const milestones = parseMilestonePage(value);
      return milestones?.personId === id ? milestones : null;
    },
  });
}
