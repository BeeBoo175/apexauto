"use client";

import { useContext } from "react";
import { useRouter } from "next/navigation";
import { Calendar } from "@/components/ui/calendar/Calendar";
import { Button } from "@/components/ui/Button";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { DataTablePagination } from "@/components/ui/data-table/DataTablePagination";
import { useWorkCalendar } from "@/hooks/use-work-calendar";
import { useGuardedAction } from "@/hooks/use-guarded-action";
import type { WorkCalendarEntry } from "@/hooks/use-work-calendar-entry";
import type { CurrentActor } from "@/lib/auth/actor";
import { calendarDate } from "@/lib/calendar";
import {
  workCalendarEvents,
  workItemHref,
  workItemKind,
  workItemLabel,
} from "@/lib/staff/work-calendar-presentation";
import { StaffCommandBar } from "./StaffPage";
import { StaffNavigationContext, StaffPageEntranceContext } from "./StaffActorBoundary";
import { StaffLink } from "./StaffLink";
import { WorkCalendarFilters } from "./WorkCalendarFilters";
import layout from "./StaffWorkspace.module.css";
import styles from "./WorkCalendar.module.css";

export function WorkCalendarWorkspace({
  actor,
  entry,
}: {
  actor: CurrentActor;
  entry: WorkCalendarEntry;
}) {
  const calendar = useWorkCalendar(actor, entry);
  const { scheduled, unscheduled, pending } = calendar;
  const animate = useContext(StaffPageEntranceContext);
  const navigation = useContext(StaffNavigationContext);
  const router = useRouter();
  const guard = useGuardedAction();
  const busy = pending.scheduled || pending.unscheduled;
  const own = actor.role === "SalesAgent" || actor.role === "WorkshopTechnician";
  return (
    <div className={styles.workspace}>
      <LoadingProgress label="Loading work" active={busy || pending.branches} />
      <StaffCommandBar
        title="Work calendar"
        actions={
          <RefreshButton
            label="Refresh work calendar"
            busy={busy}
            disabled={busy || calendar.branchRequired}
            onClick={() => void calendar.refresh()}
          />
        }
      />
      <div className={`${styles.content} ${animate ? layout.reveal : ""}`}>
        <WorkCalendarFilters actor={actor} calendar={calendar} />
        {calendar.branchRequired ? (
          <p className={styles.empty}>Choose a branch to view its work calendar.</p>
        ) : (
          <>
            <section aria-label="Scheduled work" aria-busy={pending.scheduled}>
              {scheduled.data && (
                <ScopeCaption
                  employee={scheduled.data.employee}
                  branch={scheduled.data.items[0]?.branchName}
                  own={own}
                />
              )}
              <Calendar
                label="Work calendar"
                events={scheduled.data ? workCalendarEvents(scheduled.data) : []}
                visibleDate={scheduled.data ? scheduled.filters.date : calendar.filters.date}
                confirmed={Boolean(scheduled.data)}
                today={calendarDate()}
                locale="en-MY"
                timeZone="Asia/Kuala_Lumpur"
                busy={pending.scheduled}
                toolbar={
                  !scheduled.data && (
                    <Button
                      size="compact"
                      variant="secondary"
                      disabled={pending.scheduled}
                      onClick={() => void calendar.loadScheduled()}
                    >
                      Retry scheduled work
                    </Button>
                  )
                }
                onMonthChange={(date) => void calendar.changeMonth(date)}
                onEventSelect={(event) => {
                  if (event.data) {
                    const href = workItemHref(event.data);
                    guard.request(() => {
                      navigation?.recordNavigation(href);
                      router.push(href);
                    });
                  }
                }}
              />
            </section>
            <section
              className={styles.unscheduled}
              aria-label="Unscheduled work"
              aria-busy={pending.unscheduled}
            >
              <h2>Unscheduled work</h2>
              {unscheduled.data ? (
                <>
                  <ScopeCaption
                    employee={unscheduled.data.employee}
                    branch={unscheduled.data.items[0]?.branchName}
                    own={own}
                  />
                  {unscheduled.data.items.length === 0 && (
                    <p className={styles.empty}>No unscheduled work on this page.</p>
                  )}
                  <ul className={styles.items}>
                    {unscheduled.data.items.map((item) => (
                      <li key={`${item.kind}:${item.id}`}>
                        <StaffLink
                          href={workItemHref(item)}
                          prefetch={false}
                          className={styles.item}
                        >
                          <strong>{item.registrationPlate}</strong>
                          <span>{item.vehicle}</span>
                          <span>
                            {workItemKind(item)} · {workItemLabel(item)}
                          </span>
                          <span>{item.handlerEmail}</span>
                        </StaffLink>
                      </li>
                    ))}
                  </ul>
                  <DataTablePagination
                    page={unscheduled.data}
                    count={unscheduled.data.items.length}
                    disabled={pending.unscheduled}
                    onPage={(page, pageSize) => void calendar.changePage(page, pageSize)}
                  />
                </>
              ) : (
                <div className={styles.empty}>
                  <Button
                    size="compact"
                    variant="secondary"
                    disabled={pending.unscheduled}
                    onClick={() => void calendar.loadUnscheduled()}
                  >
                    Retry unscheduled work
                  </Button>
                </div>
              )}
            </section>
          </>
        )}
      </div>
    </div>
  );
}

function ScopeCaption({
  employee,
  branch,
  own,
}: {
  employee: string;
  branch?: string;
  own: boolean;
}) {
  let employeeLabel = employee || "All employees";
  if (own) {
    employeeLabel = "My work";
  } else if (employee === "unassigned") {
    employeeLabel = "Unassigned";
  }
  return (
    <p className={styles.scope}>
      {branch ? `${branch} · ` : ""}
      {employeeLabel}
    </p>
  );
}
