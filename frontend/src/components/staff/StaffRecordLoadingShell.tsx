import workbench from "./StaffWorkspace.module.css";
import styles from "./StaffRecordLoadingShell.module.css";

export function StaffRecordLoadingShell({
  label,
  details,
  showForm = false,
}: {
  label: string;
  details: readonly string[];
  showForm?: boolean;
}) {
  return (
    <div>
      <p role="status" className="sr-only">
        {label}
      </p>
      <div className={workbench.workbench} aria-hidden="true">
        <div className={workbench.recordHeading}>
          <span className={`${styles.placeholder} ${styles.registration}`} />
          <span className={`${styles.placeholder} ${styles.status}`} />
        </div>
        <dl className={workbench.details}>
          {details.map((label) => (
            <div key={label}>
              <dt>
                <span className={`${styles.placeholder} ${styles.label}`} />
              </dt>
              <dd>
                <span
                  className={`${styles.placeholder} ${label === "Reference" ? styles.reference : styles.value}`}
                />
              </dd>
            </div>
          ))}
        </dl>
        {showForm && (
          <div className={`${workbench.protocol} ${styles.protocol}`}>
            <span className={`${styles.placeholder} ${styles.heading}`} />
            <span className={`${styles.placeholder} ${styles.value}`} />
            <span className={`${styles.placeholder} ${styles.field}`} />
            <span className={`${styles.placeholder} ${styles.value}`} />
            <span className={`${styles.placeholder} ${styles.value}`} />
            <span className={`${styles.placeholder} ${styles.action}`} />
          </div>
        )}
      </div>
    </div>
  );
}
