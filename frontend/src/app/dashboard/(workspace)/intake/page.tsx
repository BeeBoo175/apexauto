import { StaffEntry } from "@/components/staff/StaffEntry";
import { actorIdentity, hasIntakeAccess } from "@/lib/auth/actor";
import { staffBranches } from "@/lib/staff/server";
import { isUuid } from "@/lib/auth/contracts";
import { privateApiRead } from "@/lib/server/http/api";
import { parsePage } from "@/lib/api";
import { parseDetail, parseWorklistItem } from "@/lib/staff/parse";
import { staffParams, staffQuery } from "@/lib/staff/query";
import { IntakeWorkspace } from "@/components/staff/IntakeWorkspace";
import { IntakeCalendar } from "@/components/staff/IntakeCalendar";
import { IntakeWorklist } from "@/components/staff/IntakeWorklist";
import type { SearchParams } from "@/lib/query";
import {
  calendarParams,
  calendarQuery,
  calendarReadRange,
  parseCalendar,
} from "@/lib/staff/calendar";
import { calendarDate } from "@/lib/calendar";

export default async function IntakePage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  return (
    <StaffEntry
      path="/dashboard/intake"
      access={hasIntakeAccess}
      deniedTitle="Intake access unavailable"
      deniedDescription="This role cannot access intake appointments."
    >
      {async (actor) => {
        const params = await searchParams;
        const { filters, page } = staffParams(params);
        const calendarFilters = calendarParams(params);
        const listView = params.view === "list";
        const selectedId = isUuid(params.appointment) ? params.appointment : "";
        const [initial, calendar, branches, selected] = await Promise.all([
          listView
            ? privateApiRead(
                `/api/staff/intake/appointments?${staffQuery(filters, page)}`,
                (value) => parsePage(value, parseWorklistItem),
              )
            : null,
          !listView
            ? privateApiRead(
                `/api/staff/intake/calendar?${calendarQuery(calendarFilters)}`,
                (value) => parseCalendar(value, calendarReadRange(calendarFilters)),
                undefined,
                1024 * 1024,
              )
            : null,
          staffBranches(actor),
          selectedId
            ? privateApiRead(`/api/staff/intake/appointments/${selectedId}`, parseDetail)
            : null,
        ]);
        return (
          <IntakeWorkspace
            key={actorIdentity(actor)}
            actor={actor}
            today={calendarDate()}
            initialView={
              listView
                ? { view: "list", Component: IntakeWorklist }
                : { view: "calendar", Component: IntakeCalendar }
            }
            initial={{
              view: listView ? "list" : "calendar",
              worklist: initial,
              calendar,
              filters,
              page,
              calendarFilters,
              branches: branches?.data ?? null,
              selectedId,
              selected,
            }}
          />
        );
      }}
    </StaffEntry>
  );
}
