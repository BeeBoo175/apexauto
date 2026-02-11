import type { ReactNode } from "react";
import styles from "./FilterSection.module.css";

export function FilterSection({
  title,
  action,
  children,
}: {
  title: string;
  action?: ReactNode;
  children: ReactNode;
}) {
  return (
    <section className={styles.section} role="group" aria-label={title}>
      <div className={styles.heading}>
        <h3>{title}</h3>
        {action}
      </div>
      <div className={styles.content}>{children}</div>
    </section>
  );
}
