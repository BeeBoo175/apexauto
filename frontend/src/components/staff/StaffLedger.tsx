import type { ReactNode } from "react";
import workbench from "./StaffWorkspace.module.css";

// A titled run of label and value rows divided by hairlines, as the workshop's repair and finding details are.
export function StaffLedger({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className={workbench.stack} aria-label={title}>
      <div className={workbench.sectionHeader}>
        <h2 className={workbench.sectionTitle}>{title}</h2>
      </div>
      <dl className={workbench.ledger}>{children}</dl>
    </section>
  );
}
