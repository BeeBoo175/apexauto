import type { ReactNode } from "react";
import styles from "./StaffWorkspace.module.css";

// The one header every staff drawer and record workspace opens with: a labelled headline value on the left,
// the status group and secondary context on the right. Facts about the record sit under the headline;
// children are notices and links across the full width beneath.
export function StaffRecordSummary({
  ariaLabel,
  label,
  value,
  facts,
  status,
  context,
  busy,
  children,
}: {
  ariaLabel: string;
  label: ReactNode;
  value: ReactNode;
  facts?: ReactNode;
  status?: ReactNode;
  context?: ReactNode;
  busy?: boolean;
  children?: ReactNode;
}) {
  return (
    <section className={styles.summary} aria-label={ariaLabel} aria-busy={busy || undefined}>
      <div className={styles.summaryMain}>
        <div>
          <span className={styles.summaryLabel}>{label}</span>
          <strong className={styles.summaryValue}>{value}</strong>
          {facts}
        </div>
        <div className={styles.summaryContext}>
          {status}
          {context}
        </div>
      </div>
      {children}
    </section>
  );
}

// A labelled fact under the headline, such as the handling agent.
export function StaffSummaryFact({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className={styles.summaryFact}>
      <span className={styles.summaryLabel}>{label}</span>
      {children}
    </div>
  );
}
