import table from "@/components/ui/data-table/DataTable.module.css";
import toolbar from "@/components/ui/RecordToolbar.module.css";
import styles from "./StaffLoadingShell.module.css";

const rows = Array.from({ length: 8 }, (_, index) => index);
const cells = Array.from({ length: 6 }, (_, index) => index);

// The loading shape of a staff table page: queue tabs, the search and export row, the header, rows as tall as
// the real ones and the paging footer.
export function StaffTableLoadingShell() {
  return (
    <div className={`${table.workspace} ${table.fillHeight}`} aria-hidden="true">
      <div className={styles.tabsRow}>
        {[0, 1, 2, 3].map((tab) => (
          <span key={tab} className={styles.tabBar} />
        ))}
      </div>
      <div className={toolbar.toolbar}>
        <div className={toolbar.toolbarMain}>
          <span className={styles.search} />
          <span className={styles.control} />
        </div>
      </div>
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
