"use client";

import { useContext, useState } from "react";
import { useRouter } from "next/navigation";
import { Calendar } from "@/components/ui/calendar/Calendar";
import { Button } from "@/components/ui/Button";
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
import layout from "./StaffWorkspace.module.css";
import styles from "./WorkCalendar.module.css";

export function WorkCalendarWorkspace({
  actor,
  entry,
}: {
  actor: CurrentActor;
  entry: WorkCalendarEntry;
}) {
  const [scheduledKind, setScheduledKind] = useState<"" | "Intake" | "TestDrive">("");
  const calendar = useWorkCalendar(actor, entry);
  const { scheduled, unscheduled, pending } = calendar;
  const animate = useContext(StaffPageEntranceContext);
  const navigation = useContext(StaffNavigationContext);
  const router = useRouter();
  const guard = useGuardedAction();
  const busy = pending.scheduled || pending.unscheduled;
  const own = actor.role === "SalesAgent";
  let kinds: ("Workshop" | "Sale")[] = ["Workshop", "Sale"];
  if (actor.role === "SalesAgent") {
    kinds = ["Sale"];
  }
  const appointments = scheduled.data?.items;
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
            onClick={() => void calendar.refresh()}
          />
        }
      />
      <WorkCalendarFilters actor={actor} calendar={calendar} />
      <div className={`${styles.content} ${animate ? layout.reveal : ""}`}>
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
          onRetry={() => void calendar.loadUnscheduled()}
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
                {!scheduled.data && (
                  <Button
                    size="compact"
                    variant="secondary"
                    disabled={pending.scheduled}
                    onClick={() => void calendar.loadScheduled()}
                  >
                    Retry scheduled work
                  </Button>
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
