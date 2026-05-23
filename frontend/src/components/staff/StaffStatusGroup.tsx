import type { ReactNode } from "react";
import styles from "./StaffStatusGroup.module.css";

export function StaffStatusGroup({
  label = "Record status",
  children,
  attention,
}: {
  label?: string;
  children: ReactNode;
  attention?: ReactNode;
}) {
  return (
    <div className={styles.group} role="group" aria-label={label}>
      <div className={styles.badges}>{children}</div>
      {attention && (
        <div className={styles.badges} role="group" aria-label="Additional conditions">
          {attention}
        </div>
      )}
    </div>
  );
}
