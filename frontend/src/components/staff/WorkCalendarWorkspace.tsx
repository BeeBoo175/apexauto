"use client";

import { useContext, useState } from "react";
import { ViewSwitcher } from "@/components/ui/ViewSwitcher";
import { Select } from "@/components/ui/Input";
import { ReloadNotice } from "@/components/ui/ReloadNotice";
import { useRouter } from "next/navigation";
import { Calendar } from "@/components/ui/calendar/Calendar";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { useWorkCalendar } from "@/hooks/use-work-calendar";
import { useGuardedAction } from "@/hooks/use-guarded-action";
import type { WorkCalendarEntry } from "@/hooks/use-work-calendar-entry";
import type { CurrentActor } from "@/lib/auth/actor";
import { calendarDate } from "@/lib/calendar";
import { workCalendarEvents, workItemHref } from "@/lib/staff/work-calendar-presentation";
import { StaffCommandBar } from "./StaffPage";
import { StaffNavigationContext, StaffPageEntranceContext } from "./StaffActorBoundary";
import { WorkCalendarFilters } from "./WorkCalendarFilters";
import { StaffQueueTabs } from "./StaffQueueTabs";
import { OngoingWorkQueue } from "./OngoingWorkQueue";
import { UnassignedDayMark, WorkCalendarEvent } from "./WorkCalendarEvent";
import layout from "./StaffWorkspace.module.css";
import styles from "./WorkCalendar.module.css";

export function WorkCalendarWorkspace({
  actor,
  entry,
}: {
  actor: CurrentActor;
  entry: WorkCalendarEntry;
}) {
  const [mobilePanel, setMobilePanel] = useState<"appointments" | "ongoing">("appointments");
  const [scheduledKind, setScheduledKind] = useState<"" | "Intake" | "TestDrive">("");
  const calendar = useWorkCalendar(actor, entry);
  const { scheduled, unscheduled, pending } = calendar;
  const animate = useContext(StaffPageEntranceContext);
  const navigation = useContext(StaffNavigationContext);
  const router = useRouter();
  const guard = useGuardedAction();
  const busy = pending.scheduled || pending.unscheduled;
  const own = actor.role === "SalesAgent";
  let kinds: ("workshop" | "sale")[] = ["workshop", "sale"];
  if (actor.role === "SalesAgent") {
    kinds = ["sale"];
  }
  const appointments = scheduled.data?.items;
  // The general manager's ordered branch list fixes each branch's colour; other roles see one branch.
  const branchOrder =
    calendar.branches?.items.map((branch) => branch.id) ??
    Array.from(new Set(appointments?.map((item) => item.branchId)));
  const tabs = [
    { value: "", label: "All appointments", count: appointments?.length },
    {
      value: "Intake",
      label: "Intake",
      count: appointments?.filter((item) => item.kind === "Intake").length,
    },
    {
      value: "TestDrive",
      label: "Test drives",
      count: appointments?.filter((item) => item.kind === "TestDrive").length,
    },
  ] as const;

  return (
    <div className={styles.workspace}>
      <LoadingProgress label="Loading work" active={busy || pending.branches} />
      <StaffCommandBar
        title="Work calendar"
        actions={
          <RefreshButton
            label="Refresh work calendar"
            busy={busy}
            disabled={busy}
            onClick={() => {
              void calendar.refresh();
            }}
          />
        }
      />
      <WorkCalendarFilters actor={actor} calendar={calendar} />
      <div className={styles.mobileNavigation}>
        <ViewSwitcher
          label="Work calendar view"
          appearance="surface"
          labelled
          value={mobilePanel}
          options={[
            { value: "appointments", label: "Appointments", icon: "calendar" },
            { value: "ongoing", label: "Ongoing work", icon: "clipboard-list" },
          ]}
          onChange={(panel) => {
            setMobilePanel(panel);
            window.scrollTo({ top: 0 });
          }}
        />
      </div>
      <div
        className={`${styles.content} ${animate ? layout.reveal : ""}`}
        data-mobile-panel={mobilePanel}
      >
        <OngoingWorkQueue
          snapshot={unscheduled.data}
          busy={pending.unscheduled}
          own={own}
          showBranch={actor.role === "GeneralManager" && !unscheduled.filters.branchId}
          search={unscheduled.filters.search}
          kind={unscheduled.filters.kind}
          kinds={kinds}
          onKind={(kind) => void calendar.changeWork(kind)}
          onPage={(page, pageSize) => void calendar.changePage(page, pageSize)}
        />
        <div className={styles.scheduled} aria-label="Scheduled work" aria-busy={pending.scheduled}>
          <header className={styles.panelToolbar}>
            <h2 className="sr-only">Appointments</h2>
            <StaffQueueTabs
              label="Appointment types"
              countNoun="appointments"
              tabs={tabs}
              value={scheduledKind}
              onChange={setScheduledKind}
            />
          </header>
          <Calendar
            label="Work calendar"
            mobileHint={
              <>
                <span className={styles.legendKey}>
                  <span className={styles.appointmentDot} aria-hidden="true" />
                  Appointments
                </span>
                {!own && (
                  <span className={styles.warningHint}>
                    <span className={styles.unassignedDot} aria-hidden="true" />
                    Unassigned
                  </span>
                )}
              </>
            }
            renderDayMark={(events) => <UnassignedDayMark events={events} />}
            renderEvent={(event, { compact }) => (
              <WorkCalendarEvent
                event={event}
                compact={compact}
                branchOrder={branchOrder}
                showBranch={scheduled.data?.branchId === null}
              />
            )}
            events={
              scheduled.data
                ? workCalendarEvents({
                    ...scheduled.data,
                    items: scheduled.data.items.filter(
                      (item) => !scheduledKind || item.kind === scheduledKind,
                    ),
                  })
                : []
            }
            visibleDate={scheduled.data ? scheduled.filters.date : calendar.filters.date}
            confirmed={Boolean(scheduled.data)}
            today={calendarDate()}
            locale="en-MY"
            timeZone="Asia/Kuala_Lumpur"
            busy={pending.scheduled}
            fit
            toolbar={
              <>
                {!scheduled.data && <ReloadNotice subject="Scheduled work" />}
                {actor.role !== "SalesAgent" && (
                  <div className={styles.appointmentFilters}>
                    <Select
                      label="Appointment assignment"
                      labelHidden
                      density="compact"
                      value={scheduled.filters.assignment}
                      disabled={pending.scheduled}
                      onChange={(event) => {
                        const assignment = event.target.value;
                        if (
                          assignment === "all" ||
                          assignment === "assigned" ||
                          assignment === "unassigned"
                        ) {
                          void calendar.changeAssignment(assignment);
                        }
                      }}
                    >
                      <option value="all">Any assignment</option>
                      <option value="assigned">Assigned</option>
                      <option value="unassigned">Unassigned</option>
                    </Select>
                  </div>
                )}
              </>
            }
            onMonthChange={(date) => void calendar.changeMonth(date)}
            onEventSelect={(event) => {
              if (!event.data) {
                return;
              }
              const href = workItemHref(event.data);
              guard.request(() => {
                navigation?.recordNavigation(href);
                router.push(href);
              });
            }}
          />
        </div>
      </div>
    </div>
  );
}
