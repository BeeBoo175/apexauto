import { calendarTime } from "@/lib/calendar";
import tones from "@/components/ui/StatusTone.module.css";
import type { CalendarEvent, CalendarProps } from "./types";
import styles from "./CalendarEvents.module.css";

export function CalendarEventButton<Data>({
  event,
  timeZone,
  locale,
  disabled,
  renderEvent,
  onEventSelect,
  compact = false,
}: Pick<
  CalendarProps<Data>,
  "timeZone" | "locale" | "disabled" | "renderEvent" | "onEventSelect"
> & {
  event: CalendarEvent<Data>;
  compact?: boolean;
}) {
  const time = calendarTime(event.startsAt, timeZone, locale);
  let displayTime = time;
  if (compact && !renderEvent) {
    displayTime = calendarTime(event.startsAt, timeZone, locale, false);
  }

  return (
    <button
      type="button"
      className={`${styles.event} ${tones[event.tone ?? "info"]}`}
      data-tone={event.tone ?? "info"}
      data-compact={(compact && !renderEvent) || undefined}
      title={`${event.title}, ${time}${event.label ? `, ${event.label}` : ""}`}
      aria-label={`Open ${event.title}, ${time}${event.label ? `, ${event.label}` : ""}`}
      aria-disabled={disabled || undefined}
      onClick={() => {
        if (!disabled) {
          onEventSelect(event);
        }
      }}
    >
      {renderEvent ? (
        renderEvent(event)
      ) : (
        <>
          <time dateTime={event.startsAt}>{displayTime}</time>
          <strong>{event.title}</strong>
          {event.label && <span>{event.label}</span>}
        </>
      )}
    </button>
  );
}
