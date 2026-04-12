import type { ReactNode } from "react";
import styles from "./VehiclePurchaseSummary.module.css";

export function VehiclePurchaseSummary({
  total,
  breakdown,
  promo,
  financing,
}: {
  total: string;
  breakdown: { label: string; amount: string }[];
  promo?: ReactNode;
  financing?: ReactNode;
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
        <p>Insurance and optional warranty are excluded.</p>
      </div>
      {promo}
      <div className={styles.total} aria-live="polite" aria-atomic="true">
        <span>Estimated total</span>
        <strong>{total}</strong>
      </div>
      {financing}
    </div>
  );
}
