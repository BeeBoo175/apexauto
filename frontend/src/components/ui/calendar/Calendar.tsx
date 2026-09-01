"use client";

import { useMemo, useState } from "react";
import {
  addCalendarDays,
  addCalendarMonths,
  calendarDate,
  calendarDayLabel,
  calendarRange,
} from "@/lib/calendar";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { CalendarMonth } from "./CalendarMonth";
import { CalendarEventButton } from "./CalendarEventButton";
import type { CalendarProps } from "./types";
import styles from "./Calendar.module.css";

export function Calendar<Data>(props: CalendarProps<Data>) {
  const { events, timeZone } = props;
  const month = props.visibleDate.slice(0, 7);
  const first = `${month}-01`;
  const preferred = props.today.startsWith(month) ? props.today : first;
  const afterMinimum = props.minDate && preferred < props.minDate ? props.minDate : preferred;
  const initialDate = props.maxDate && afterMinimum > props.maxDate ? props.maxDate : afterMinimum;
  const scope = `${month}:${props.minDate ?? ""}:${props.maxDate ?? ""}`;
  const [selection, setSelection] = useState({ scope, date: initialDate });
  const selectedDate = selection.scope === scope ? selection.date : initialDate;
  const grouped = useMemo(() => {
    const days = new Map<string, (typeof events)[number][]>();
    const ordered = [...events].sort(
      (left, right) =>
        Date.parse(left.startsAt) - Date.parse(right.startsAt) || left.id.localeCompare(right.id),
    );
    const range = calendarRange(first);
    for (const event of ordered) {
      const start = calendarDate(new Date(event.startsAt), timeZone);
      const end = calendarDate(new Date(Date.parse(event.endsAt) - 1), timeZone);
      const from = start < range.from ? range.from : start;
      const to = end > range.to ? range.to : end;
      for (let date = from; date <= to; date = addCalendarDays(date, 1)) {
        const dayEvents = days.get(date) ?? [];
        dayEvents.push(event);
        days.set(date, dayEvents);
      }
    }
    return days;
  }, [events, timeZone, first]);
  const selectedEvents = grouped.get(selectedDate) ?? [];
  const heading = new Intl.DateTimeFormat(props.locale, {
    month: "long",
    year: "numeric",
    timeZone: "UTC",
  }).format(new Date(`${first}T00:00:00Z`));
  const shortHeading = new Intl.DateTimeFormat(props.locale, {
    month: "short",
    year: "numeric",
    timeZone: "UTC",
  }).format(new Date(`${first}T00:00:00Z`));

  return (
    <section
      aria-label={props.label}
      aria-busy={props.busy}
      data-fit={props.fit || undefined}
      className={styles.calendar}
    >
      <div className={styles.toolbar}>
        <h2 aria-live="polite" aria-label={heading} title={heading}>
          <span className={styles.fullMonth} aria-hidden="true">
            {heading}
          </span>
          <span className={styles.shortMonth} aria-hidden="true">
            {shortHeading}
          </span>
        </h2>
        <div className={styles.navigation}>
          <Button
            size="compact"
            variant="secondary"
            className={props.busy ? styles.readPending : undefined}
            disabled={
              props.disabled ||
              props.busy ||
              Boolean(props.minDate && props.today < props.minDate) ||
              Boolean(props.maxDate && props.today > props.maxDate)
            }
            onClick={() => {
              setSelection({ scope, date: props.today });
              props.onMonthChange(props.today);
            }}
          >
            Today
          </Button>
          <Button
            size="icon"
            variant="ghost"
            aria-label="Previous month"
            className={props.busy ? styles.readPending : undefined}
            disabled={
              props.disabled ||
              props.busy ||
              month === "0002-01" ||
              Boolean(props.minDate && month <= props.minDate.slice(0, 7))
            }
            onClick={() => props.onMonthChange(addCalendarMonths(first, -1))}
          >
            <Icon name="previous" />
          </Button>
          <Button
            size="icon"
            variant="ghost"
            aria-label="Next month"
            className={props.busy ? styles.readPending : undefined}
            disabled={
              props.disabled ||
              props.busy ||
              month === "9998-12" ||
              Boolean(props.maxDate && month >= props.maxDate.slice(0, 7))
            }
            onClick={() => props.onMonthChange(addCalendarMonths(first, 1))}
          >
            <Icon name="next" />
          </Button>
        </div>
        {props.toolbar && <div className={styles.tools}>{props.toolbar}</div>}
        <div className={styles.mobileHint} aria-label="Calendar legend">
          {props.mobileHint ?? "Select a date for details."}
        </div>
      </div>
      <CalendarMonth
        key={month}
        {...props}
        selectedDate={selectedDate}
        grouped={grouped}
        onDateSelect={(date) => setSelection({ scope, date })}
      />
      <section className={styles.datePanel} aria-label="Selected date events">
        <h3>{calendarDayLabel(selectedDate, props.locale)}</h3>
        {props.confirmed !== false && (
          <p className={styles.count} role="status">
            {selectedEvents.length} {selectedEvents.length === 1 ? "event" : "events"}
          </p>
        )}
        {props.confirmed !== false && selectedEvents.length === 0 && <p>No events on this date.</p>}
        <div className={styles.eventList}>
          {selectedEvents.map((event) => (
            <CalendarEventButton key={event.id} {...props} event={event} />
          ))}
        </div>
      </section>
    </section>
  );
}
