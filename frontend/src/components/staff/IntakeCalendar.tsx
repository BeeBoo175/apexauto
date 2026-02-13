"use client";

import { Calendar } from "@/components/ui/calendar/Calendar";
import { calendarEvents } from "@/lib/staff/calendar-presentation";
import type { useStaffCalendar } from "@/hooks/use-staff-calendar";
import type { useStaffAppointmentDrawer } from "@/hooks/use-staff-appointment-drawer";
import type { CurrentActor } from "@/lib/auth/actor";
import type { CalendarFilters as Filters } from "@/lib/staff/calendar";
import { CalendarFilters } from "./CalendarFilters";
import styles from "./IntakeCalendar.module.css";

interface Props {
  actor: CurrentActor;
  today: string;
  calendar: ReturnType<typeof useStaffCalendar>;
  drawer: ReturnType<typeof useStaffAppointmentDrawer>;
}

export function IntakeCalendar({ actor, today, calendar, drawer }: Props) {
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
    <div
      className={styles.workspace}
      inert={!calendar.ready || calendar.busy}
      aria-busy={calendar.busy}
    >
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
