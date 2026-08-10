import type { ReactNode } from "react";
import styles from "./VehiclePurchaseSummary.module.css";

export function VehiclePurchaseSummary({
  total,
  totalNote,
  breakdown,
  promo,
  financing,
  notice,
}: {
  total: string | null;
  totalNote?: string | null;
  breakdown: { label: string; amount: string }[];
  promo?: ReactNode;
  financing?: ReactNode;
  notice?: string;
}) {
  return (
    <div className={styles.summary}>
      {total !== null && totalNote && (
        <div className={styles.totalInline} aria-live="polite" aria-atomic="true">
          <span className={styles.totalAmount}>
            Estimated total <strong>{total}</strong>
          </span>
          <span className={styles.totalNote}>{totalNote}</span>
        </div>
      )}
      <div className={styles.disclosure}>
        <dl className={styles.breakdown}>
          {breakdown.map((row) => (
            <div key={row.label} data-discount={row.amount.startsWith("−") || undefined}>
              <dt>{row.label}</dt>
              <dd>{row.amount}</dd>
            </div>
          ))}
        </dl>
        {total !== null && <p>Insurance and optional warranty are excluded.</p>}
      </div>
      {promo}
      {total !== null && !totalNote && (
        <div className={styles.total} aria-live="polite" aria-atomic="true">
          <span>Estimated total</span>
          <strong>{total}</strong>
        </div>
      )}
      {notice && <p className={styles.notice}>{notice}</p>}
      {financing && <div className={styles.actions}>{financing}</div>}
    </div>
  );
}
