import styles from "./WorkshopLoadingShell.module.css";

export function WorkshopLoadingShell({ label = "Loading workshop record…" }: { label?: string }) {
  return (
    <>
      <p role="status" className="sr-only">
        {label}
      </p>
      <div className={styles.shell} aria-hidden="true">
        <div className={styles.summary}>
          <span className={styles.title} />
          <span className={styles.context} />
        </div>
        <div className={styles.sections}>
          <span className={styles.line} />
        </div>
        <div className={styles.body}>
          <span className={styles.index} />
          <div className={styles.detail}>
            <span className={styles.title} />
            <span className={styles.block} />
            <span className={styles.block} />
          </div>
        </div>
        <div className={styles.footer}>
          <span className={styles.line} />
        </div>
      </div>
    </>
  );
}
