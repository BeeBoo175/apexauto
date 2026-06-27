import calendar from "@/components/ui/calendar/Calendar.module.css";
import month from "@/components/ui/calendar/CalendarMonth.module.css";
import toolbar from "@/components/ui/RecordToolbar.module.css";
import styles from "./StaffLoadingShell.module.css";
import work from "./WorkCalendar.module.css";
import queue from "./StaffQueueWorkspace.module.css";

const weekdays = Array.from({ length: 7 }, (_, index) => index);
const weeks = Array.from({ length: 6 }, (_, index) => index);

function TabStrip({ count, narrow = false }: { count: number; narrow?: boolean }) {
  return (
    <div className={work.panelToolbar}>
      <div
        className={`${styles.tabsRow} ${styles.tabsRowBare} ${narrow ? styles.tabsRowNarrow : ""}`}
      >
        {Array.from({ length: count }, (_, tab) => (
          <span key={tab} className={styles.tabBar} />
        ))}
      </div>
    </div>
  );
}

// The loading shape of the work calendar: the shared search and filter row, the queue of unscheduled work with
// its tabs and paging, and the month beneath its appointment tabs.
export function WorkCalendarLoadingShell() {
  return (
    <div className={work.workspace} aria-hidden="true">
      <div className={work.sharedFilters}>
        <div className={toolbar.toolbar}>
          <div className={toolbar.toolbarMain}>
            <span className={styles.wideSearch} />
            <span className={styles.calendarFilter} />
          </div>
        </div>
      </div>
      <div className={work.content}>
        <aside className={work.ongoing}>
          <TabStrip count={3} narrow />
          <div className={queue.list}>
            {[0, 1, 2, 3].map((item) => (
              <div key={item} className={styles.queueJob}>
                <div className={styles.queueJobHeading}>
                  <span className={styles.monthTitle} />
                  <span className={styles.badgeBar} />
                </div>
                <span className={styles.shortValue} />
                <div className={styles.queueJobPerson}>
                  <span className={styles.avatarBar} />
                  <span className={styles.shortValue} />
                </div>
              </div>
            ))}
          </div>
          <div className={queue.queueFooter}>
            <div className={styles.queueJob}>
              <span className={styles.shortValue} />
            </div>
          </div>
        </aside>
        <div className={work.scheduled}>
          <TabStrip count={3} />
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
            </div>
            <div className={month.month} data-fit="true">
              <div className={month.grid}>
                <div className={month.weekdays}>
                  <span className={styles.weekdayBand} />
                </div>
                <div className={month.weeks}>
                  {weeks.map((week) => (
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
          </div>
        </div>
      </div>
    </div>
  );
}
