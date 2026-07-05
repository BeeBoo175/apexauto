import { useEffect, useState } from "react";
import { StaffLink } from "@/components/staff/StaffLink";
import { PerformanceSection, type SectionState } from "@/components/performance/PerformanceSection";
import performance from "@/components/performance/Performance.module.css";
import { hourLabel } from "@/lib/performance/format";
import { calendarTime } from "@/lib/calendar";
import type { ScheduledWork } from "@/lib/staff/work-calendar";
import { workItemHref } from "@/lib/staff/work-calendar-presentation";
import styles from "./Home.module.css";

const timeZone = "Asia/Kuala_Lumpur";
const malaysiaOffsetHours = 8;
const calendarHref = "/dashboard/work-calendar";

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

function byHour(items: ScheduledWork[]) {
  const hours = new Map<number, ScheduledWork[]>();
  for (const item of items) {
    const hour = malaysiaHour(item.startsAt);
    hours.set(hour, [...(hours.get(hour) ?? []), item]);
  }
  return [...hours.entries()].sort(([first], [second]) => first - second);
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
    <PerformanceSection
      title="Today's schedule"
      aside={
        items && (
          <span className={performance.panelAside}>
            {scheduleSummary(items)},{" "}
            <StaffLink href={calendarHref}>open the work calendar</StaffLink>
          </span>
        )
      }
      state={{ ...state, hasData: items !== null }}
      placeholder={<span className={`${performance.placeholder} ${styles.shellTimeline}`} />}
    >
      {items && items.length === 0 && <p className={performance.note}>Nothing is booked today.</p>}
      {items && items.length > 0 && <HourRows items={items} showBranch={showBranch} />}
    </PerformanceSection>
  );
}

function HourRows({ items, showBranch }: { items: ScheduledWork[]; showBranch: boolean }) {
  const now = useNowHour();
  return (
    <ol className={styles.hoursList} aria-label="Today's bookings by hour">
      {byHour(items).map(([hour, bookings]) => (
        <li key={hour} className={styles.hourRow} data-past={hour < now}>
          <span className={styles.hourLabel}>
            {hourLabel(hour)}
            {hour === now && <span className={styles.nowTag}>Now</span>}
          </span>
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
                    {showBranch ? `, ${item.branchName}` : ""}
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
