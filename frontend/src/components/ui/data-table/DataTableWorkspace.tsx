import type { ReactNode } from "react";
import styles from "./DataTable.module.css";

export function DataTableWorkspace({ label, children }: { label: string; children: ReactNode }) {
  return (
    <section className={`${styles.workspace} ${styles.fillHeight}`} aria-label={label}>
      {children}
    </section>
  );
}
