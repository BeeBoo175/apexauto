import table from "@/components/ui/data-table/DataTable.module.css";
import toolbar from "@/components/ui/RecordToolbar.module.css";
import styles from "./StaffLoadingShell.module.css";

const rows = Array.from({ length: 8 }, (_, index) => index);
const cells = Array.from({ length: 6 }, (_, index) => index);
const lenses = Array.from({ length: 4 }, (_, index) => index);

// The loading shape of a staff table page: queue tabs, the search and export row, the header, rows as tall as
// the real ones and the paging footer. Phones get the shape of the comparative mobile table instead: search, the
// Sort and Filter pair, the column tabs and two-column rows.
export function StaffTableLoadingShell() {
  return (
    <div className={`${table.workspace} ${table.fillHeight}`} aria-hidden="true">
      <div className={styles.tabsRow}>
        {[0, 1, 2, 3].map((tab) => (
          <span key={tab} className={styles.tabBar} />
        ))}
      </div>
      <div className={styles.desktopOnly}>
        <div className={toolbar.toolbar}>
          <div className={toolbar.toolbarMain}>
            <span className={styles.search} />
            <span className={styles.control} />
          </div>
        </div>
      </div>
      <div className={`${styles.mobileToolbar} ${styles.mobileOnly}`}>
        <span className={styles.search} />
        <div className={styles.mobilePair}>
          <span className={styles.control} />
          <span className={styles.control} />
        </div>
      </div>
      <div className={`${styles.mobileLenses} ${styles.mobileOnly}`}>
        {lenses.map((lens) => (
          <span key={lens} className={styles.tabBar} />
        ))}
      </div>
      <div className={`${styles.mobileTable} ${styles.mobileOnly}`}>
        <div className={styles.mobileHeading}>
          <span className={styles.shortValue} />
          <span className={styles.shortValue} />
        </div>
        {rows.map((row) => (
          <div key={row} className={styles.mobileRow}>
            <div className={styles.mobileIdentity}>
              <span className={styles.cellBar} />
              <span className={styles.cellBar} />
              <span className={styles.shortValue} />
            </div>
            <span className={styles.mobileMetric} />
          </div>
        ))}
      </div>
      <div className={styles.desktopOnly}>
        <div className={table.scroll}>
          <div className={styles.table}>
            <span className={styles.tableHeading} />
            {rows.map((row) => (
              <div key={row} className={styles.tableRow}>
                {cells.map((cell) => (
                  <span key={cell} className={styles.cellBar} />
                ))}
              </div>
            ))}
          </div>
        </div>
      </div>
      <div className={table.pagination}>
        <span className={styles.monthTitle} />
        <div className={table.paginationControls}>
          <span className={styles.control} />
          <span className={styles.iconControl} />
          <span className={styles.iconControl} />
        </div>
      </div>
    </div>
  );
}
