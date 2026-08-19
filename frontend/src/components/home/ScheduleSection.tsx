import { useEffect, useState } from "react";
import { StaffLink } from "@/components/staff/StaffLink";
import { EmptyNotice } from "@/components/ui/EmptyNotice";
import { DashboardSection, type SectionState } from "@/components/staff/DashboardSection";
import dashboard from "@/components/staff/DashboardSection.module.css";
import { branchLabel } from "@/lib/branches";
import { hourLabel } from "@/lib/calendar";
import { calendarTime } from "@/lib/calendar";
import type { ScheduledWork } from "@/lib/staff/work-calendar";
import { workItemHref } from "@/lib/staff/work-calendar-presentation";
import styles from "./Home.module.css";

const timeZone = "Asia/Kuala_Lumpur";
const malaysiaOffsetHours = 8;
const calendarHref = "/dashboard/work-calendar";
// Branch bookings run from the first slot to the last, so every hour between them shows even when it is free.
const firstWorkingHour = 9;
const lastWorkingHour = 17;

const kindLabels: Record<ScheduledWork["kind"], string> = {
  Intake: "Intake",
  TestDrive: "Test drive",
};

// The Malaysian clock hour of a moment; Malaysia keeps one offset all year.
function malaysiaHour(stamp: string | number) {
  return new Date(new Date(stamp).getTime() + malaysiaOffsetHours * 3600000).getUTCHours();
}

// The current Malaysian hour, moved on each minute, so past hours can be told from upcoming ones.
function useNowHour() {
  const [now, setNow] = useState(() => malaysiaHour(Date.now()));
  useEffect(() => {
    const timer = window.setInterval(() => setNow(malaysiaHour(Date.now())), 60000);
    return () => window.clearInterval(timer);
  }, []);
  return now;
}

// Every working hour in order, with its bookings; an unusual booking outside the usual day widens the rail.
function dayRail(items: ScheduledWork[]) {
  const booked = new Map<number, ScheduledWork[]>();
  for (const item of items) {
    const hour = malaysiaHour(item.startsAt);
    booked.set(hour, [...(booked.get(hour) ?? []), item]);
  }
  const hours = [...booked.keys()];
  const first = Math.min(firstWorkingHour, ...hours);
  const last = Math.max(lastWorkingHour, ...hours);
  return Array.from({ length: last - first + 1 }, (_, index) => {
    const hour = first + index;
    return [hour, booked.get(hour) ?? []] as const;
  });
}

function scheduleSummary(items: ScheduledWork[]) {
  if (items.length === 0) {
    return "No bookings today";
  }
  const unassigned = items.filter((item) => !item.handlerId).length;
  const booked = `${items.length} ${items.length === 1 ? "booking" : "bookings"}`;
  return unassigned === 0
    ? `${booked}, all assigned`
    : `${booked}, ${unassigned} with no one assigned`;
}

// Today's appointments and test drives from the Work calendar, one row per booked hour. Hours already past are
// quieter, and a booking with no one assigned says so in words.
export function ScheduleSection({
  items,
  state,
  showBranch,
}: {
  items: ScheduledWork[] | null;
  state: SectionState;
  showBranch: boolean;
}) {
  return (
    <DashboardSection
      title="Today's schedule"
      aside={
        items &&
        items.length > 0 && (
          <span className={dashboard.panelAside}>
            {scheduleSummary(items)},{" "}
            <StaffLink href={calendarHref}>open the work calendar</StaffLink>
          </span>
        )
      }
      state={{ ...state, hasData: items !== null }}
      placeholder={<span className={`${dashboard.placeholder} ${styles.shellTimeline}`} />}
    >
      {items && items.length === 0 && (
        <div className={styles.ghost}>
          <div className={styles.ghostRail} aria-hidden="true">
            <HourRows items={items} showBranch={showBranch} />
          </div>
          <div className={styles.ghostMessage}>
            <div className={styles.ghostCard}>
              <EmptyNotice icon="calendar" title="No bookings today">
                Appointments and test drives appear here.
              </EmptyNotice>
              <StaffLink href={calendarHref} className={styles.ghostLink}>
                Open the work calendar
              </StaffLink>
            </div>
          </div>
        </div>
      )}
      {items && items.length > 0 && <HourRows items={items} showBranch={showBranch} />}
    </DashboardSection>
  );
}

function HourRows({ items, showBranch }: { items: ScheduledWork[]; showBranch: boolean }) {
  const now = useNowHour();
  return (
    <ol className={styles.hoursList} aria-label="Today's bookings by hour">
      {dayRail(items).map(([hour, bookings]) => (
        <li
          key={hour}
          className={styles.hourRow}
          data-past={hour < now}
          data-free={bookings.length === 0}
        >
          <span className={styles.hourLabel}>
            {hourLabel(hour)}
            {hour === now && <span className={styles.nowTag}>Now</span>}
          </span>
          {bookings.length === 0 && <span className={styles.freeHour}>Open</span>}
          <ul className={styles.chips}>
            {bookings.map((item) => (
              <li key={`${item.kind}:${item.id}`}>
                <StaffLink
                  href={workItemHref(item)}
                  className={`${styles.booking} ${item.kind === "Intake" ? styles.bookingIntake : styles.bookingDrive}`}
                >
                  <span className={styles.bookingTop}>
                    <span className={styles.bookingTime}>
                      {calendarTime(item.startsAt, timeZone)}
                    </span>
                    {kindLabels[item.kind]}
                  </span>
                  <span className={styles.bookingLine}>
                    {item.registrationPlate}
                    {showBranch ? `, ${branchLabel(item.branchName)}` : ""}
                  </span>
                  <span
                    className={`${styles.bookingLine} ${item.handlerId ? "" : styles.unassigned}`.trim()}
                  >
                    {item.handlerName ?? "No one assigned"}
                  </span>
                </StaffLink>
              </li>
            ))}
          </ul>
        </li>
      ))}
    </ol>
  );
}
