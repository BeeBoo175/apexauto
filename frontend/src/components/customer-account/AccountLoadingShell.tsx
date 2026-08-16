import styles from "./AccountLoadingShell.module.css";

const rows = Array.from({ length: 4 }, (_, index) => index);

// The first-entry silhouette of an account history list: the rows only. Later reads keep the confirmed rows on
// screen instead, so this never replaces content that has already loaded.
export function AccountLoadingShell({ label }: { label: string }) {
  return (
    <div>
      <p role="status" className="sr-only">
        {label}
      </p>
      <div className={styles.list} aria-hidden="true">
        <span className={`${styles.placeholder} ${styles.summary}`} />
        {rows.map((row) => (
          <div key={row} className={styles.row}>
            <span className={`${styles.placeholder} ${styles.title}`} />
            <span className={`${styles.placeholder} ${styles.detail}`} />
            <span className={`${styles.placeholder} ${styles.badge}`} />
          </div>
        ))}
      </div>
    </div>
  );
}
