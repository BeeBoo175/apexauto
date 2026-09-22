import type { ReactNode } from "react";
import { RESERVATION_HOLD_HOURS } from "@/lib/vehicles/deposit-terms";
import { formatCurrency } from "@/lib/currency-format";
import { DEFAULT_CASH_PERCENT } from "@/lib/vehicles/financing";
import layout from "./VehicleLayout.module.css";
import styles from "./VehiclePriceBox.module.css";

interface VehiclePriceBoxProps {
  name: string;
  showroom: string;
  price: string;
  total: string;
  totalNote: string;
  monthly: { amount: number; years: number } | null;
  breakdown: { label: string; amount: string; discount?: boolean }[];
  actions: ReactNode;
  promo: ReactNode;
}

// The sticky box beside the listing: name, showroom, price, the actions, the refund line, the fees, then
// the promo code as an opt-in disclosure.
export function VehiclePriceBox({
  name,
  showroom,
  price,
  total,
  totalNote,
  monthly,
  breakdown,
  actions,
  promo,
}: VehiclePriceBoxProps) {
  return (
    <div id="vehicle-price-box" className={styles.box}>
      <div className={styles.identity}>
        <h1 className={layout.pageHeading}>{name}</h1>
        <p className={styles.showroom}>{showroom}</p>
      </div>

      <div className={styles.price}>
        <strong>{price}</strong>
        <span>Vehicle price</span>
      </div>

      <div className={styles.total} aria-live="polite" aria-atomic="true">
        <span>
          On the road <strong>{total}</strong>
        </span>
        <span className={styles.note}>{totalNote}</span>
        {monthly && (
          <span className={styles.note}>
            About {formatCurrency(monthly.amount)} a month over {monthly.years} years,{" "}
            {DEFAULT_CASH_PERCENT}% cash. Not a loan offer.
          </span>
        )}
      </div>

      <div id="vehicle-actions" className={styles.actions}>
        {actions}
      </div>

      <p className={styles.refund}>
        The deposit is refunded in full if you cancel inside the {RESERVATION_HOLD_HOURS}-hour hold
        or let it lapse.
      </p>

      <dl className={styles.breakdown}>
        {breakdown.map((row) => (
          <div key={row.label} data-discount={row.discount || undefined}>
            <dt>{row.label}</dt>
            <dd>{row.amount}</dd>
          </div>
        ))}
      </dl>
      <p className={styles.note}>Insurance and optional warranty are excluded.</p>

      <details className={styles.promo}>
        <summary>Have a promo code?</summary>
        {promo}
      </details>
    </div>
  );
}
