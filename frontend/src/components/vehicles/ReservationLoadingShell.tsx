import styles from "./ReservationLoadingShell.module.css";

export function ReservationLoadingShell() {
  return (
    <div className={styles.shell} aria-hidden="true">
      <div className={styles.summary}>
        <span className={styles.total} />
        <span className={styles.status} />
      </div>
      <div className={styles.body}>
        {[0, 1].map((group) => (
          <div className={styles.section} key={group}>
            <span className={styles.heading} />
            <div className={styles.facts}>
              {Array.from({ length: 6 }, (_, index) => (
                <div className={styles.fact} key={index}>
                  <span className={styles.label} />
                  <span className={styles.value} />
                </div>
              ))}
            </div>
          </div>
        ))}
      </div>
      <div className={styles.footer}>
        <span className={styles.value} />
      </div>
    </div>
  );
}
