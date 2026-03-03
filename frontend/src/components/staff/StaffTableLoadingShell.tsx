import table from "@/components/ui/data-table/DataTable.module.css";
import toolbar from "@/components/ui/RecordToolbar.module.css";
import styles from "./StaffLoadingShell.module.css";

export function StaffTableLoadingShell() {
  return (
    <div className={`${table.workspace} ${table.fillHeight}`} aria-hidden="true">
      <div className={toolbar.toolbar}>
        <div className={toolbar.toolbarMain}>
          <span className={styles.search} />
          <span className={styles.control} />
        </div>
      </div>
      <div className={table.scroll}>
        <div className={styles.table}>
          <span className={styles.tableHeading} />
          {Array.from({ length: 5 }, (_, index) => (
            <div key={index} className={styles.tableRow}>
              <span className={styles.value} />
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
