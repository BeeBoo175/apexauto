import { useState, type KeyboardEvent } from "react";
import { calendarWeeks } from "@/lib/calendar";
import { CalendarDay } from "./CalendarDay";
import { CalendarDayPopover, type CalendarDaySelection } from "./CalendarDayPopover";
import type { CalendarEvent, CalendarProps } from "./types";
import styles from "./CalendarMonth.module.css";

interface MonthProps<Data> extends CalendarProps<Data> {
  selectedDate: string;
  grouped: ReadonlyMap<string, CalendarEvent<Data>[]>;
  onDateSelect: (date: string) => void;
}

export function CalendarMonth<Data>(props: MonthProps<Data>) {
  const [selection, setSelection] = useState<CalendarDaySelection | null>(null);
  const weeks = calendarWeeks(props.visibleDate);
  const weekday = new Intl.DateTimeFormat(props.locale, { weekday: "short", timeZone: "UTC" });

  function selectDay(cell: HTMLElement, trigger: HTMLButtonElement) {
    const date = cell.dataset.date;
    if (props.disabled || props.busy || !date) {
      return;
    }
    props.onDateSelect(date);
    if (window.matchMedia("(min-width: 821px)").matches) {
      setSelection({ date, cell, trigger });
    }
  }

  return (
    <div
      className={styles.month}
      data-fit={props.fit || undefined}
      onKeyDown={focusDate}
      onClickCapture={(event) => {
        const target = event.target as HTMLElement;
        const cell = target.closest<HTMLElement>('[role="cell"][data-date]');
        if (!cell || target.closest("[data-calendar-event]")) {
          return;
        }
        const trigger = cell.querySelector<HTMLButtonElement>("[data-date-control]");
        if (trigger) {
          selectDay(cell, trigger);
        }
      }}
    >
      <div role="table" aria-label="Month dates" className={styles.grid}>
        <div role="row" className={styles.weekdays}>
          {weeks[0].map((date) => (
            <div key={date} role="columnheader" className={styles.weekday}>
              {weekday.format(new Date(`${date}T00:00:00Z`))}
            </div>
          ))}
        </div>
        <div role="rowgroup" className={styles.weeks}>
          {weeks.map((week) => (
            <div key={week[0]} role="row" className={styles.week}>
              {week.map((date) => (
                <CalendarDay
                  key={date}
                  {...props}
                  date={date}
                  events={props.grouped.get(date) ?? []}
                  selected={date === props.selectedDate}
                  active={date === selection?.date}
                />
              ))}
            </div>
          ))}
        </div>
      </div>
      {selection && (
        <CalendarDayPopover
          key={selection.date}
          {...props}
          selection={selection}
          events={props.grouped.get(selection.date) ?? []}
          onClose={() => setSelection(null)}
        />
      )}
    </div>
  );
}

function focusDate(event: KeyboardEvent<HTMLDivElement>) {
  const target = event.target as HTMLElement;
  if (!target.matches("[data-date-control]")) {
    return;
  }
  const dates = [...event.currentTarget.querySelectorAll<HTMLButtonElement>("[data-date-control]")];
  const index = dates.indexOf(target as HTMLButtonElement);
  const offsets: Record<string, number> = {
    ArrowLeft: -1,
    ArrowRight: 1,
    ArrowUp: -7,
    ArrowDown: 7,
    Home: -(index % 7),
    End: 6 - (index % 7),
  };
  const offset = offsets[event.key];
  if (offset === undefined) {
    return;
  }
  event.preventDefault();
  dates[index + offset]?.focus();
}
