"use client";

import { useRef, useState, type CSSProperties } from "react";
import { createPortal } from "react-dom";
import type { CalendarEvent } from "@/components/ui/calendar/types";
import type { ScheduledWork } from "@/lib/staff/work-calendar";
import { calendarTime } from "@/lib/calendar";
import { branchLabel } from "@/lib/branches";
import { branchPosition } from "@/lib/staff/work-calendar-branch";
import { workItemKind, workItemLabel } from "@/lib/staff/work-calendar-presentation";
import { Icon } from "@/components/ui/Icon";
import tones from "@/components/ui/StatusTone.module.css";
import styles from "./WorkCalendarEvent.module.css";

const inactiveStatuses = ["Cancelled", "NoShow"];
const HOVER_DELAY_MS = 250;
const CARD_WIDTH = 320;
const CARD_HEIGHT = 96;
const CARD_GAP = 4;

interface EntryProps {
  event: CalendarEvent<ScheduledWork>;
  showBranch?: boolean;
  branchOrder: readonly string[];
  timeZone?: string;
  locale?: string;
}

function entryFacts({
  event,
  branchOrder,
  timeZone = "Asia/Kuala_Lumpur",
  locale = "en-MY",
}: EntryProps) {
  const data = event.data;
  const start = calendarTime(event.startsAt, timeZone, locale, true);
  const end = event.endsAt ? calendarTime(event.endsAt, timeZone, locale, true) : null;
  return {
    data,
    branch: data?.branchName ? branchLabel(data.branchName) : "Branch",
    branchPosition: branchPosition(data?.branchId, branchOrder),
    branchCode: data?.branchShortCode ?? "",
    kind: data ? workItemKind(data) : "Appointment",
    statusText: data ? workItemLabel(data) : (event.label ?? "Scheduled"),
    plate: data?.registrationPlate ?? event.title,
    isInactive: Boolean(data && inactiveStatuses.includes(data.status)),
    // Cancelled and no-show work never needs staff, so it is not reported as unassigned.
    needsStaff: !data?.handlerId && !(data && inactiveStatuses.includes(data.status)),
    range: end ? `${start} – ${end}` : start,
    shortStart: calendarTime(event.startsAt, timeZone, locale, false),
  };
}

function EntryRow(props: EntryProps) {
  const { event } = props;
  const facts = entryFacts(props);
  const { data } = facts;
  return (
    <div className={styles.row}>
      <span className={styles.rowPrimary}>
        <time className={styles.rowTime} dateTime={event.startsAt}>
          {facts.range}
        </time>
        <strong className={styles.rowPlate}>{facts.plate}</strong>
        <span className={`${styles.rowStatus} ${tones.badge} ${tones[event.tone ?? "info"]}`}>
          {facts.statusText}
        </span>
      </span>
      <span className={styles.rowSecondary}>
        <span className={styles.rowVehicle}>
          {data ? `${data.vehicle} · ${facts.kind}` : facts.kind}
        </span>
      </span>
      <span className={styles.rowSecondary}>
        <span className={styles.rowBranch} data-branch={facts.branchPosition % 3}>
          {facts.branch}
        </span>
        <span className={styles.rowHandler} data-unassigned={facts.needsStaff || undefined}>
          {facts.needsStaff ? "Unassigned" : data?.handlerName}
        </span>
      </span>
    </div>
  );
}

// Month cells are too narrow for detail, so hovering a chip shows the same row the day popover uses.
function CompactChip(props: EntryProps) {
  const chip = useRef<HTMLDivElement>(null);
  const timer = useRef<number | undefined>(undefined);
  const [cardStyle, setCardStyle] = useState<CSSProperties | null>(null);
  const facts = entryFacts(props);
  const data = facts.data;
  // Open work nobody has taken outranks its status colour: amber means someone needs to claim it.
  const { isInactive, needsStaff } = facts;

  function openCard() {
    window.clearTimeout(timer.current);
    timer.current = window.setTimeout(() => {
      const rect = chip.current?.getBoundingClientRect();
      if (!rect) {
        return;
      }
      const fitsBelow = rect.bottom + CARD_GAP + CARD_HEIGHT < window.innerHeight;
      const left = Math.max(8, Math.min(rect.left, window.innerWidth - CARD_WIDTH - 8));
      setCardStyle({
        left,
        top: fitsBelow ? rect.bottom + CARD_GAP : rect.top - CARD_GAP - CARD_HEIGHT,
      });
    }, HOVER_DELAY_MS);
  }

  function closeCard() {
    window.clearTimeout(timer.current);
    setCardStyle(null);
  }

  return (
    <div
      ref={chip}
      className={`${styles.compactChip} ${tones[needsStaff ? "warning" : (props.event.tone ?? "info")]}`}
      data-inactive={isInactive || undefined}
      data-unassigned={needsStaff || undefined}
      onMouseEnter={openCard}
      onMouseLeave={closeCard}
    >
      <time className={styles.compactTime} dateTime={props.event.startsAt}>
        {facts.shortStart}
      </time>
      <span className={styles.compactPlate}>{facts.plate}</span>
      <span className={styles.compactBranch} data-shown={props.showBranch || undefined}>
        {facts.branchCode}
      </span>
      <span className={styles.compactKind}>
        <Icon name={data?.kind === "TestDrive" ? "car" : "clipboard-list"} />
      </span>
      <span className={styles.compactStatus}>{facts.statusText}</span>
      {needsStaff && <span className="sr-only">Unassigned</span>}
      {cardStyle &&
        createPortal(
          <div className={styles.hoverCard} style={cardStyle} aria-hidden="true">
            <EntryRow {...props} />
          </div>,
          document.body,
        )}
    </div>
  );
}

export function WorkCalendarEvent({
  compact,
  ...props
}: EntryProps & {
  compact: boolean;
}) {
  return compact ? <CompactChip {...props} /> : <EntryRow {...props} />;
}

export function UnassignedDayMark({ events }: { events: readonly CalendarEvent<ScheduledWork>[] }) {
  const count = events.filter(
    (event) => !event.data?.handlerId && !inactiveStatuses.includes(event.data?.status ?? ""),
  ).length;
  if (count === 0) {
    return null;
  }
  return (
    <span className={styles.dayMark} data-unassigned="" title={`${count} unassigned`}>
      <Icon name="alert-triangle" />
      <span aria-hidden="true">{count}</span>
      <span className="sr-only">{count} unassigned</span>
    </span>
  );
}
