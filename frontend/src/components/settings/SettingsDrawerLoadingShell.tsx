import styles from "./SettingsDrawerLoadingShell.module.css";

export function SettingsDrawerLoadingShell({ fields }: { fields: number }) {
  return (
    <div className={styles.shell} aria-hidden="true">
      <div className={styles.body}>
        {Array.from({ length: fields }, (_, index) => (
          <div className={styles.field} key={index}>
            <span className={styles.label} />
            <span className={styles.control} />
          </div>
        ))}
      </div>
      <div className={styles.footer}>
        <span className={styles.action} />
      </div>
    </div>
  );
}
