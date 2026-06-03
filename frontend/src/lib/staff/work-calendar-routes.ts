import "server-only";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import { DEFAULT_PAGE_SIZE } from "@/lib/pagination";
import { apiError, apiJson, privateApiRead } from "@/lib/server/http/api";
import { accessDenied, allowedQuery, confirmedActor } from "@/lib/server/http/staff-relay";
import {
  hasWorkCalendarAccess,
  parseUnscheduledWork,
  parseWorkCalendar,
  type UnscheduledWorkPage,
  type WorkCalendarSnapshot,
} from "./work-calendar";

export async function relayWorkCalendar(request: Request, unscheduled = false) {
  try {
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(20_000)]);
    const actor = await confirmedActor(signal);
    if (!hasWorkCalendarAccess(actor.role)) {
      return accessDenied(actor, "Operational calendar access required.");
    }
    const keys = unscheduled ? ["page", "pageSize"] : ["from", "to"];
    const query = allowedQuery(new URL(request.url).searchParams, [
      ...keys,
      "branchId",
      "employee",
    ]);
    const scopeMatches = (data: { actor: CurrentActor; branchId: string; employee: string }) =>
      actorIdentity(data.actor) === actorIdentity(actor) &&
      data.branchId === (query.get("branchId") || actor.branchId) &&
      data.employee === (query.get("employee") ?? "").trim();
    const result = unscheduled
      ? await privateApiRead(
          `/api/staff/work-calendar/unscheduled?${query}`,
          (value) => {
            const page = parseUnscheduledWork(value);
            return page &&
              scopeMatches(page) &&
              page.page === Number(query.get("page") ?? 1) &&
              page.pageSize === Number(query.get("pageSize") ?? DEFAULT_PAGE_SIZE)
              ? page
              : null;
          },
          signal,
        )
      : await privateApiRead(
          `/api/staff/work-calendar?${query}`,
          (value) => {
            const calendar = parseWorkCalendar(value);
            return calendar &&
              scopeMatches(calendar) &&
              calendar.from === query.get("from") &&
              calendar.to === query.get("to")
              ? calendar
              : null;
          },
          signal,
          1024 * 1024,
        );
    return apiJson<WorkCalendarSnapshot | UnscheduledWorkPage>({
      ...result,
      actor: result.data?.actor ?? actor,
    });
  } catch (error) {
    return apiError(error);
  }
}
