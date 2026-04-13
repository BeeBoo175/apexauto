import type { ReactNode } from "react";
import styles from "./VehiclePurchaseSummary.module.css";

export function VehiclePurchaseSummary({
  total,
  breakdown,
  promo,
  financing,
  notice,
}: {
  total: string | null;
  breakdown: { label: string; amount: string }[];
  promo?: ReactNode;
  financing?: ReactNode;
  notice?: string;
}) {
  return (
    <div className={styles.summary}>
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
      {total !== null && (
        <div className={styles.total} aria-live="polite" aria-atomic="true">
          <span>Estimated total</span>
          <strong>{total}</strong>
        </div>
      )}
      {notice && <p className={styles.notice}>{notice}</p>}
      {financing}
    </div>
  );
}
