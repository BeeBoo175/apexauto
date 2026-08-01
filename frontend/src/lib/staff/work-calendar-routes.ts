import "server-only";
import type { CurrentActor } from "@/lib/auth/actor";
import { DEFAULT_PAGE_SIZE } from "@/lib/pagination";
import { relayGatedRead } from "@/lib/server/http/staff-relay";
import { hasWorkCalendarAccess, parseUnscheduledWork, parseWorkCalendar } from "./work-calendar";

export function relayWorkCalendar(request: Request, unscheduled = false) {
  const keys = unscheduled ? ["page", "pageSize", "kind"] : ["from", "to", "assignment"];
  return relayGatedRead(request, {
    dataFirst: true,
    allowed: (actor) => hasWorkCalendarAccess(actor.role),
    denial: "Operational calendar access required.",
    path: unscheduled ? "/api/staff/work-calendar/unscheduled" : "/api/staff/work-calendar",
    keys: [...keys, "branchId", "search"],
    deadlineMs: 20_000,
    maximumResponseBytes: unscheduled ? undefined : 1024 * 1024,
    parse: (value, query) => {
      const scopeMatches = (data: {
        actor: CurrentActor;
        branchId: string | null;
        employee: string;
      }) =>
        data.branchId === (query.get("branchId") || data.actor.branchId) &&
        (data.actor.role === "GeneralManager" || data.branchId === data.actor.branchId) &&
        data.employee === "";
      if (unscheduled) {
        const page = parseUnscheduledWork(value);
        return page &&
          scopeMatches(page) &&
          page.page === Number(query.get("page") ?? 1) &&
          page.pageSize === Number(query.get("pageSize") ?? DEFAULT_PAGE_SIZE)
          ? page
          : null;
      }
      const calendar = parseWorkCalendar(value);
      return calendar &&
        scopeMatches(calendar) &&
        calendar.from === query.get("from") &&
        calendar.to === query.get("to")
        ? calendar
        : null;
    },
  });
}
