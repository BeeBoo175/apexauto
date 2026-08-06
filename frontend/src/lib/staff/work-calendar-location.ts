import { calendarDate } from "@/lib/calendar";
import { DEFAULT_PAGE_SIZE } from "@/lib/pagination";
import { workCalendarParams, type WorkCalendarFilters } from "./work-calendar";

export const workCalendarWorkspace = "/dashboard/work-calendar";

// Branch, work kind and assignment narrow the view only for the roles that can choose them.
export function scopeWorkCalendarFilters(filters: WorkCalendarFilters, role: string) {
  const general = role === "GeneralManager";
  const manager = general || role === "BranchManager";
  return {
    ...filters,
    branchId: general ? filters.branchId : "",
    assignment: manager ? filters.assignment : ("all" as const),
    kind: manager ? filters.kind : ("" as const),
  };
}

// Shape: /dashboard/work-calendar[?branchId=&search=&view=workshop|sale&assignment=assigned|unassigned&date=&page=
// &pageSize=]. A default (today, all work, first page, default size) never appears in the address.
export function workCalendarHref(filters: WorkCalendarFilters) {
  const query = new URLSearchParams();
  const values = {
    branchId: filters.branchId,
    search: filters.search.trim(),
    view: filters.kind,
    assignment: filters.assignment === "all" ? "" : filters.assignment,
    date: filters.date === calendarDate() ? "" : filters.date,
    page: filters.page > 1 ? String(filters.page) : "",
    pageSize: filters.pageSize === DEFAULT_PAGE_SIZE ? "" : String(filters.pageSize),
  };
  for (const [key, value] of Object.entries(values)) {
    if (value) {
      query.set(key, value);
    }
  }
  return query.size ? `${workCalendarWorkspace}?${query}` : workCalendarWorkspace;
}

export function workCalendarLocation(pathname: string, search: string, role: string) {
  if (pathname !== workCalendarWorkspace) {
    return null;
  }
  const filters = scopeWorkCalendarFilters(
    workCalendarParams(Object.fromEntries(new URLSearchParams(search))),
    role,
  );
  return { filters, href: workCalendarHref(filters) };
}
