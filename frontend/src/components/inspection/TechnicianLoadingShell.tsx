import { WorkshopLoadingShell } from "./WorkshopLoadingShell";
import workspace from "./TechnicianWorkspace.module.css";
import styles from "./TechnicianLoadingShell.module.css";
import loading from "@/components/staff/StaffLoadingShell.module.css";

export function TechnicianLoadingShell({ selected }: { selected: boolean }) {
  return (
    <div className={workspace.workspace} aria-hidden="true">
      <div className={workspace.columns} data-selected={selected}>
        <div className={workspace.queue} data-expanded={!selected}>
          {selected && (
            <div className={workspace.mobileQueueBar}>
              <span className={styles.navigation} />
            </div>
          )}
          <div className={workspace.queueBody}>
            <div className={workspace.queueNavigation}>
              <span className={styles.navigation} />
            </div>
            <div className={workspace.list}>
              <div className={workspace.search}>
                <span className={loading.search} />
              </div>
              <div className={styles.jobs}>
                <span className={styles.job} />
                <span className={styles.job} />
                <span className={styles.job} />
              </div>
            </div>
          </div>
        </div>
        {selected ? (
          <div className={workspace.content}>
            <div className={workspace.contentHeader}>
              <div className={styles.identity}>
                <span className={styles.title} />
                <span className={styles.context} />
              </div>
            </div>
            <div className={workspace.editor}>
              <WorkshopLoadingShell />
            </div>
          </div>
        ) : (
          <div className={workspace.placeholder}>
            <span className={styles.title} />
            <span className={styles.context} />
          </div>
        )}
      </div>
    </div>
  );
}
