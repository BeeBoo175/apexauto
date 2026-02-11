import { useLayoutEffect, useRef } from "react";
import { calendarDayLabel } from "@/lib/calendar";
import { CloseButton } from "../CloseButton";
import { CalendarEventButton } from "./CalendarEventButton";
import { dayPopoverPosition } from "./day-popover-position";
import type { CalendarEvent, CalendarProps } from "./types";
import styles from "./CalendarDayPopover.module.css";

export interface CalendarDaySelection {
  date: string;
  cell: HTMLElement;
  trigger: HTMLElement;
}

export function CalendarDayPopover<Data>({
  selection,
  events,
  onClose,
  ...props
}: Omit<CalendarProps<Data>, "events"> & {
  selection: CalendarDaySelection;
  events: CalendarEvent<Data>[];
  onClose: () => void;
}) {
  const popup = useRef<HTMLDivElement>(null);
  const list = useRef<HTMLDivElement>(null);
  const heading = calendarDayLabel(selection.date, props.locale);

  useLayoutEffect(() => {
    const element = popup.current!;
    const place = () => {
      if (window.matchMedia("(max-width: 820px)").matches) {
        element.hidePopover();
        return;
      }
      const cell = selection.cell.getBoundingClientRect();
      if (
        cell.bottom <= 0 ||
        cell.top >= innerHeight ||
        cell.right <= 0 ||
        cell.left >= innerWidth
      ) {
        element.hidePopover();
        return;
      }
      const bounds = element.getBoundingClientRect();
      const contentHeight =
        element.offsetHeight + list.current!.scrollHeight - list.current!.clientHeight;
      const position = dayPopoverPosition(
        cell,
        { width: bounds.width, contentHeight },
        { width: innerWidth, height: innerHeight },
      );
      element.style.left = `${position.left}px`;
      element.style.top = position.top === null ? "auto" : `${position.top}px`;
      element.style.bottom = position.bottom === null ? "auto" : `${position.bottom}px`;
      element.style.maxHeight = `${position.maxHeight}px`;
    };
    const dismissOutside = (event: MouseEvent) => {
      const target = event.target as Node;
      if (!element.contains(target) && !selection.cell.contains(target)) {
        element.hidePopover();
      }
    };
    element.showPopover();
    place();
    element.focus({ preventScroll: true });
    const observer = new ResizeObserver(place);
    observer.observe(element);
    observer.observe(selection.cell);
    window.addEventListener("resize", place);
    window.addEventListener("scroll", place, true);
    document.addEventListener("mousedown", dismissOutside);
    return () => {
      observer.disconnect();
      window.removeEventListener("resize", place);
      window.removeEventListener("scroll", place, true);
      document.removeEventListener("mousedown", dismissOutside);
      element.hidePopover();
    };
  }, [selection]);

  return (
    <div
      ref={popup}
      // Manual mode preserves this list while the shared modal appointment drawer is open.
      popover="manual"
      role="dialog"
      tabIndex={-1}
      aria-label={heading}
      className={styles.popover}
      onToggle={(event) => {
        if (event.newState === "closed") {
          onClose();
        }
      }}
      onKeyDown={(event) => {
        if (event.key === "Escape") {
          event.stopPropagation();
          event.preventDefault();
          popup.current?.hidePopover();
          selection.trigger.focus({ preventScroll: true });
        }
      }}
    >
      <div className={styles.header}>
        <h3>{heading}</h3>
        <CloseButton
          label="Close day events"
          onClick={() => {
            popup.current?.hidePopover();
            selection.trigger.focus({ preventScroll: true });
          }}
        />
      </div>
      <div ref={list} role="list" aria-label="Day events" className={styles.events} tabIndex={0}>
        {events.length === 0 && <p>No events on this date.</p>}
        {events.map((event) => (
          <div role="listitem" key={event.id}>
            <CalendarEventButton {...props} event={event} />
          </div>
        ))}
      </div>
    </div>
  );
}
