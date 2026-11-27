import { useId, type ReactNode } from "react";
import { Icon } from "./Icon";
import type { IconName } from "@/lib/icons";
import styles from "./StatusPage.module.css";

// The one layout for a whole page that cannot be shown: no access, not found, a service that did not answer. It
// fills the screen, or the content area under a page title; a failed section inside a page uses ReloadNotice. It states what happened in a line or two and never the rule
// behind it, so a denied page does not reveal who may open it.
export function StatusPage({
  code,
  icon,
  title,
  children,
  actions,
  tone = "info",
}: {
  code: string;
  icon: IconName;
  title: string;
  children?: ReactNode;
  actions?: ReactNode;
  tone?: "info" | "alert";
}) {
  const headingId = useId();
  return (
    <section className={styles.page} aria-labelledby={headingId}>
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
