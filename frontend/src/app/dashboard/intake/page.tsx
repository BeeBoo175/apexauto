import { StaffEntry } from "@/components/staff/StaffEntry";
import { actorIdentity, hasIntakeAccess } from "@/lib/auth/actor";
import { staffBranches } from "@/lib/staff/server";
import { isUuid } from "@/lib/auth/contracts";
import { privateIntakeRead } from "@/lib/intake/server";
import { parsePage } from "@/lib/intake/parse";
import { parseDetail, parseWorklistItem } from "@/lib/staff/parse";
import { staffParams, staffQuery } from "@/lib/staff/query";
import { IntakeWorkspace } from "@/components/staff/IntakeWorkspace";
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
            ? privateIntakeRead(
                `/api/staff/intake/appointments?${staffQuery(filters, page)}`,
                (value) => parsePage(value, parseWorklistItem),
              )
            : null,
          !listView
            ? privateIntakeRead(
                `/api/staff/intake/calendar?${calendarQuery(calendarFilters)}`,
                (value) => parseCalendar(value, calendarReadRange(calendarFilters)),
                undefined,
                1024 * 1024,
              )
            : null,
          staffBranches(actor),
          selectedId
            ? privateIntakeRead(`/api/staff/intake/appointments/${selectedId}`, parseDetail)
            : null,
        ]);
        return (
          <IntakeWorkspace
            key={actorIdentity(actor)}
            actor={actor}
            today={calendarDate()}
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
