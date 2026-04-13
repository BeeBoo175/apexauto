import type { ReactNode } from "react";
import styles from "./StaffWorkspace.module.css";

export function StaffWorkspaceFooter({
  title,
  description,
  children,
}: {
  title: string;
  description: string;
  children?: ReactNode;
}) {
  return (
    <footer className={styles.footer}>
      <div className={styles.footerContext}>
        <strong>{title}</strong>
        <span>{description}</span>
      </div>
      <div className={styles.footerActions}>{children}</div>
    </footer>
  );
}
