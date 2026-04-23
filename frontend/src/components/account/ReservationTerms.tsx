import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import type { AccountReservation } from "@/lib/vehicles/account-reservations";
import styles from "./Reservations.module.css";

export function ReservationTerms({ item }: { item: AccountReservation }) {
  const { receipt, release } = item;
  const prices = [
    ["Vehicle price", receipt.price.vehiclePrice],
    ["Processing fee", receipt.price.processingFee],
    ["Road tax", receipt.price.roadTax],
    ["Service tax", receipt.price.serviceTax],
    ["Promo discount", receipt.price.promoDiscount],
    ["Reserved total", receipt.price.total],
  ] as const;
  return (
    <div className={styles.terms}>
      <div>
        <h4>Confirmed reservation</h4>
        <dl className={styles.facts}>
          <div>
            <dt>Reference</dt>
            <dd>{receipt.id}</dd>
          </div>
          <div>
            <dt>Reserved by</dt>
            <dd>{receipt.buyer.legalName}</dd>
          </div>
          <div>
            <dt>Reserved on</dt>
            <dd>{formatMalaysianDateTime(receipt.createdAt)}</dd>
          </div>
          <div>
            <dt>Hold deadline</dt>
            <dd>{formatMalaysianDateTime(receipt.expiresAt)}</dd>
          </div>
          <div>
            <dt>Accepted promo</dt>
            <dd>{receipt.promoCode ?? "None"}</dd>
          </div>
          <div>
            <dt>Simulated deposit</dt>
            <dd>{formatCurrency(receipt.deposit)}</dd>
          </div>
        </dl>
      </div>
      <div>
        <h4>Reserved price</h4>
        <dl className={styles.facts}>
          {prices.map(([label, amount]) => (
            <div key={label}>
              <dt>{label}</dt>
              <dd>{formatCurrency(amount)}</dd>
            </div>
          ))}
        </dl>
        <p className={styles.help}>
          These confirmed prices retain your accepted promo. Insurance and optional warranty are
          excluded.
        </p>
      </div>
      {release && (
        <div>
          <h4>Simulated refund recorded</h4>
          <dl className={styles.facts}>
            <div>
              <dt>Refund reference</dt>
              <dd>{release.id}</dd>
            </div>
            <div>
              <dt>Amount</dt>
              <dd>{formatCurrency(release.refund)}</dd>
            </div>
            <div>
              <dt>Recorded</dt>
              <dd>{formatMalaysianDateTime(release.recordedAt)}</dd>
            </div>
            {release.reason && (
              <div>
                <dt>Reason</dt>
                <dd>{release.reason}</dd>
              </div>
            )}
          </dl>
        </div>
      )}
    </div>
  );
}
