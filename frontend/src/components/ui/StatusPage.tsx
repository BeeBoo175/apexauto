import { useId, type ReactNode } from "react";
import { Icon } from "./Icon";
import type { IconName } from "@/lib/icons";
import styles from "./StatusPage.module.css";

// The one component for every dead end and failed read: no access, not found, a service that did not answer. It has
// one layout everywhere and fills whatever space it is given, the whole screen at the top of the app, the content
// area under a page title, a drawer body or a dialog. It states what happened in a line or two and never the rule
// behind it, so a denied page does not reveal who may open it.
export function StatusPage({
  code,
  icon,
  title,
  children,
  actions,
  tone = "info",
  compact = false,
}: {
  code: string;
  icon: IconName;
  title: string;
  children?: ReactNode;
  actions?: ReactNode;
  tone?: "info" | "alert";
  // A smaller panel for a failed read inside a page section rather than a whole screen.
  compact?: boolean;
}) {
  const headingId = useId();
  return (
    <section
      className={styles.page}
      data-compact={compact || undefined}
      aria-labelledby={headingId}
    >
      <div className={styles.panel} data-tone={tone}>
        <p className={styles.code} aria-hidden="true">
          {code}
        </p>
        <div className={styles.road} aria-hidden="true">
          <span className={styles.marker}>
            <Icon name={icon} />
          </span>
        </div>
        <h2 id={headingId} className={styles.title}>
          {title}
        </h2>
        {children && <p className={styles.text}>{children}</p>}
        {actions && <div className={styles.actions}>{actions}</div>}
      </div>
    </section>
  );
}
