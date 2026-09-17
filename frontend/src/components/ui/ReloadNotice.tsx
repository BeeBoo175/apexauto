import { Icon } from "./Icon";
import styles from "./ReloadNotice.module.css";
import { StatusPage } from "./StatusPage";

// The one answer for content that could not be read. It never offers its own retry button: reading again is the
// browser's reload, so every section, drawer, dialog and page tells the user the same thing. Large areas get the
// shared status layout that fills the space it replaces; a single control, such as a filter's option list, gets
// the compact line so the notice never outgrows the control it stands in for.
export function ReloadNotice({ subject, compact = false }: { subject: string; compact?: boolean }) {
  if (compact) {
    return (
      <p className={styles.compact} role="alert">
        <Icon name="alert-triangle" />
        <span>{subject} could not be loaded. Reload the page to try again.</span>
      </p>
    );
  }

  return (
    <StatusPage
      tone="alert"
      code="503"
      icon="alert-triangle"
      title={`${subject} could not be loaded`}
    >
      Reload the page to try again.
    </StatusPage>
  );
}
