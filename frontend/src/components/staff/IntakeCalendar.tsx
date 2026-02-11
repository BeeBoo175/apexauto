"use client";

import { useState } from "react";
import { Calendar } from "@/components/ui/calendar/Calendar";
import { calendarEvents } from "@/lib/staff/calendar-presentation";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { useStaffCalendar } from "@/hooks/use-staff-calendar";
import { useInitialToast } from "@/hooks/use-initial-toast";
import { useStaffAppointmentDrawer } from "@/hooks/use-staff-appointment-drawer";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import type { ApiResult, Branch, Page } from "@/lib/intake/contracts";
import type { AppointmentDetail } from "@/lib/staff/contracts";
import type { CalendarFilters as Filters, CalendarSnapshot } from "@/lib/staff/calendar";
import { calendarHref } from "@/lib/staff/calendar";
import { tableViewHref } from "@/lib/staff/view-navigation";
import { CalendarFilters } from "./CalendarFilters";
import { IntakeViewNavigation } from "./IntakeViewNavigation";
import { IntakeAppointmentDrawer } from "./IntakeAppointmentDrawer";
import { StaffCommandBar } from "./StaffPage";
import styles from "./IntakeCalendar.module.css";

interface Props {
  actor: CurrentActor;
  today: string;
  initial: ApiResult<CalendarSnapshot>;
  filters: Filters;
  branches: Page<Branch> | null;
  selectedId: string;
  selected: ApiResult<AppointmentDetail> | null;
}

export function IntakeCalendar(props: Props) {
  const [navigation, setNavigation] = useState({ snapshot: props.initial, revision: 0 });
  if (navigation.snapshot !== props.initial) {
    setNavigation({ snapshot: props.initial, revision: navigation.revision + 1 });
  }
  return <CalendarWorkspace key={navigation.revision} {...props} />;
}

function CalendarWorkspace({
  actor,
  today,
  initial,
  filters,
  branches,
  selectedId,
  selected,
}: Props) {
  const calendar = useStaffCalendar(initial, filters, branches);
  useInitialToast(
    actor.role === "GeneralManager" && !branches
      ? "Branch choices are unavailable. All-branch access remains available."
      : null,
  );
  const drawer = useStaffAppointmentDrawer(selectedId, selected);
  const disabled = Boolean(drawer.id);
  const { snapshot, active } = calendar;
  const toolbar = (
    <CalendarFilters
      calendar={calendar}
      disabled={disabled}
      group={actor.role === "GeneralManager"}
    />
  );
  return (
    <>
      <div
        className={styles.workspace}
        inert={!calendar.ready || calendar.busy}
        aria-busy={calendar.busy}
      >
        <StaffCommandBar
          title="Intake appointments"
          actions={
            <>
              <RefreshButton
                label="Refresh calendar"
                busy={calendar.busy}
                disabled={disabled}
                onClick={() => void calendar.load()}
              />
              <IntakeViewNavigation
                actor={actorIdentity(actor)}
                active="calendar"
                destinations={{ calendar: calendarHref(active), list: tableViewHref(active) }}
              />
            </>
          }
        />
        {snapshot ? (
          <Calendar
            label="Intake calendar"
            events={calendarEvents(snapshot)}
            visibleDate={active.date}
            minDate={active.range?.from}
            maxDate={active.range?.to}
            today={today}
            locale="en-MY"
            timeZone="Asia/Kuala_Lumpur"
            disabled={disabled}
            busy={calendar.busy}
            fit
            toolbar={toolbar}
            onMonthChange={(date) =>
              void calendar.load({ ...active, date: calendarMonthDate(date, active) })
            }
            onEventSelect={(event) => drawer.open(event.id)}
          />
        ) : (
          <div className={styles.unavailable}>{toolbar}</div>
        )}
      </div>
      <IntakeAppointmentDrawer
        actor={actor}
        drawer={drawer}
        refreshing={calendar.busy}
        onConfirmed={() => void calendar.load()}
      />
    </>
  );
}

function calendarMonthDate(date: string, filters: Filters) {
  if (!filters.range) {
    return date;
  }
  if (date < filters.range.from) {
    return filters.range.from;
  }
  return date > filters.range.to ? filters.range.to : date;
}
