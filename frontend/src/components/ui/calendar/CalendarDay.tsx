import { useCalendarDayCapacity } from "@/hooks/use-calendar-day-capacity";
import { calendarDayLabel } from "@/lib/calendar";
import { CalendarEventButton } from "./CalendarEventButton";
import type { CalendarProps } from "./types";
import styles from "./CalendarMonth.module.css";

interface DayProps<Data> extends CalendarProps<Data> {
  date: string;
  selected: boolean;
  active: boolean;
}

export function CalendarDay<Data>({ date, events, selected, active, ...props }: DayProps<Data>) {
  const count = events.length;
  const { day, capacity } = useCalendarDayCapacity(count, 2);
  const overflow = Math.max(0, count - capacity);
  const label = `${calendarDayLabel(date, props.locale)}, ${count} ${count === 1 ? "event" : "events"}`;
  return (
    <div
      ref={day}
      role="cell"
      data-date={date}
      className={`${styles.day} ${active ? styles.activeDay : ""}`}
    >
      <div className={styles.dayTop} data-day-top="">
        {props.renderDayMark?.(events)}
        <button
          type="button"
          data-date-control=""
          className={styles.date}
          data-other={!date.startsWith(props.visibleDate.slice(0, 7)) || undefined}
          data-today={date === props.today || undefined}
          data-selected={selected || undefined}
          aria-current={date === props.today ? "date" : undefined}
          aria-label={label}
          aria-pressed={selected}
          disabled={props.disabled}
        >
          <span className={styles.dayNumber}>
            <span className={styles.dayNumberText}>{Number(date.slice(-2))}</span>
          </span>
          <span className={styles.mobileCount} aria-hidden="true">
            {count > 0 && <span className={styles.eventDot} />}
          </span>
        </button>
      </div>
      <div className={styles.events}>
        {events.slice(0, capacity).map((event) => (
          <div key={event.id} data-calendar-event="">
            <CalendarEventButton {...props} event={event} compact={props.fit} />
          </div>
        ))}
      </div>
      {overflow > 0 && <span className={styles.more}>+{overflow} more</span>}
    </div>
  );
}
