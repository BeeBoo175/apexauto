import { StaffEntry } from "@/components/staff/StaffEntry";
import { WorkCalendarClientEntry } from "@/components/staff/WorkCalendarClientEntry";
import { hasWorkCalendarAccess } from "@/lib/staff/work-calendar";
import type { SearchParams } from "@/lib/query";

export default async function WorkCalendarPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  return (
    <StaffEntry
      path="/dashboard/work-calendar"
      searchParams={searchParams}
      access={(actor) => hasWorkCalendarAccess(actor.role)}
      deniedTitle="Work calendar access unavailable"
      deniedDescription="Sales agents and branch or general managers can read the work calendar."
    >
      {async (actor) => (
        <WorkCalendarClientEntry
          key={crypto.randomUUID()}
          actor={actor}
          parameters={await searchParams}
        />
      )}
    </StaffEntry>
  );
}
