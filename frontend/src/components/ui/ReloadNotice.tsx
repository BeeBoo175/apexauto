import { useId } from "react";
import { Icon } from "./Icon";
import styles from "./ReloadNotice.module.css";

// The one answer for content that could not be read. It never offers its own retry button: reading again is the
// browser's reload, so every section, drawer and dialog tells the user the same thing. A content area gets a short
// block sized to its text; a single control, such as a filter's option list, gets the compact line so the notice
// never outgrows the control it stands in for. A whole page that could not be read uses StatusPage.
export function ReloadNotice({ subject, compact = false }: { subject: string; compact?: boolean }) {
  const headingId = useId();
  if (compact) {
    return (
      <p className={styles.compact} role="alert">
        <Icon name="alert-triangle" />
        <span>{subject} could not be loaded. Reload the page to try again.</span>
      </p>
    );
  }

  return (
    <section className={styles.section} aria-labelledby={headingId}>
      <Icon name="alert-triangle" />
      <div>
        <h2 id={headingId} className={styles.title}>
          {subject} could not be loaded
        </h2>
        <p className={styles.text}>Reload the page to try again.</p>
      </div>
    </section>
  );
}
