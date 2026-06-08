import calendar from "@/components/ui/calendar/Calendar.module.css";
import month from "@/components/ui/calendar/CalendarMonth.module.css";
import styles from "./StaffLoadingShell.module.css";
import work from "./WorkCalendar.module.css";
import queue from "./StaffQueueWorkspace.module.css";

const weekdays = Array.from({ length: 7 }, (_, index) => index);

export function WorkCalendarLoadingShell() {
  return (
    <div className={work.workspace} aria-hidden="true">
      <div className={work.sharedFilters}>
        <span className={styles.control} />
      </div>
      <div className={work.content}>
        <aside className={work.ongoing}>
          <div className={work.queueHeading}>
            <span className={styles.monthTitle} />
          </div>
          <div className={queue.list}>
            {[0, 1, 2].map((item) => (
              <div key={item} className={queue.job}>
                <span className={styles.monthTitle} />
                <span className={styles.shortValue} />
              </div>
            ))}
          </div>
        </aside>
        <div className={calendar.calendar} data-fit="true">
          <div className={calendar.toolbar}>
            <h2>
              <span className={styles.monthTitle} />
            </h2>
            <div className={calendar.navigation}>
              <span className={styles.control} />
              <span className={styles.iconControl} />
              <span className={styles.iconControl} />
            </div>
            <div className={calendar.tools}>
              <span className={styles.calendarFilter} />
            </div>
          </div>
          <div className={month.month} data-fit="true">
            <div className={month.grid}>
              <div className={month.weekdays}>
                <span className={styles.weekdayBand} />
              </div>
              <div className={month.weeks}>
                {Array.from({ length: 6 }, (_, week) => (
                  <div key={week} className={month.week}>
                    {weekdays.map((day) => (
                      <div key={day} className={`${month.day} ${styles.calendarDay}`}>
                        <span className={styles.dateValue} />
                      </div>
                    ))}
                  </div>
                ))}
              </div>
            </div>
          </div>
          <section className={calendar.datePanel}>
            <h3>
              <span className={styles.monthTitle} />
            </h3>
            <div className={calendar.eventList}>
              <span className={styles.block} />
            </div>
          </section>
        </div>
      </div>
    </div>
  );
}
