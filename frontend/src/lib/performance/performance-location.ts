import { isUuid } from "@/lib/auth/contracts";
import { staffRecordLocation } from "@/lib/staff/record-location";

export const performanceWorkspace = "/dashboard/performance";

export type PerformanceParameters = Record<string, string>;

const teamViews = ["sales", "workshop"];

// The query keys Performance owns, in the order they appear in an address. Every value is a camelCase token, a
// date, an id or free text; an empty value is the default and never appears.
const queryKeys = [
  "view",
  "period",
  "from",
  "to",
  "branchId",
  "measure",
  "search",
  "name",
  "status",
  "min",
  "sort",
  "direction",
  "page",
  "pageSize",
];

// The person whose drawer is open and its tab live in the path, which keeps the team table in the query:
// /dashboard/performance/<personId>/<branchId>[/milestones]?view=sales|workshop.
export function performanceHref(parameters: PerformanceParameters) {
  const query = new URLSearchParams();
  for (const key of queryKeys) {
    if (parameters[key]) {
      query.set(key, parameters[key]);
    }
  }
  const person = teamViews.includes(parameters.view ?? "") ? (parameters.person ?? "") : "";
  const [personId, branchId] = person.split(":");
  const segments =
    isUuid(personId) && isUuid(branchId)
      ? [personId, branchId, ...(parameters.section === "milestones" ? ["milestones"] : [])]
      : [];
  const path = [performanceWorkspace, ...segments].join("/");
  return query.size ? `${path}?${query}` : path;
}

// Route parameters of the nested pages: `[personId]/[branchId]/[milestones]`.
export function performanceSelection(segments: string[] = []): PerformanceParameters | null {
  if (segments.length === 0) {
    return {};
  }
  const [personId, branchId, section, ...extra] = segments;
  if (!isUuid(personId) || !isUuid(branchId) || extra.length > 0) {
    return null;
  }
  if (section !== undefined && section !== "milestones") {
    return null;
  }
  return { person: `${personId}:${branchId}`, ...(section ? { section } : {}) };
}

export function performanceLocation(pathname: string, search: string) {
  const record = staffRecordLocation(pathname, performanceWorkspace);
  if (!record) {
    return null;
  }
  const selection = record.id
    ? performanceSelection([record.id, record.section, ...record.trail].filter(Boolean))
    : {};
  if (!selection) {
    return null;
  }
  const parameters: PerformanceParameters = {};
  for (const key of queryKeys) {
    const value = new URLSearchParams(search).get(key);
    if (value) {
      parameters[key] = value;
    }
  }
  Object.assign(parameters, selection);
  return { parameters, href: performanceHref(parameters) };
}
