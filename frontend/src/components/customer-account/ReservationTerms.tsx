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
  ] as const;
  const facts = [
    ["Reference", receipt.number],
    ["Reserved by", receipt.buyer.legalName],
    ["Reserved on", formatMalaysianDateTime(receipt.createdAt)],
    ["Hold deadline", formatMalaysianDateTime(receipt.expiresAt)],
    ["Accepted promo", receipt.promoCode ?? "None"],
    ["Simulated deposit", formatCurrency(receipt.deposit)],
  ] as const;
  return (
    <div className={styles.terms}>
      <section>
        <h4>Reservation</h4>
        <dl className={styles.keyFacts}>
          {facts.map(([label, value]) => (
            <div key={label}>
              <dt>{label}</dt>
              <dd>{value}</dd>
            </div>
          ))}
        </dl>
      </section>
      <section>
        <h4>Price breakdown</h4>
        <dl className={styles.receipt}>
          {prices.map(([label, amount]) => (
            <div key={label}>
              <dt>{label}</dt>
              <dd>{formatCurrency(amount)}</dd>
            </div>
          ))}
          <div className={styles.receiptTotal}>
            <dt>Reserved total</dt>
            <dd>{formatCurrency(receipt.price.total)}</dd>
          </div>
        </dl>
        <p className={styles.help}>
          These confirmed prices retain your accepted promo. Insurance and optional warranty are
          excluded.
        </p>
      </section>
      {release && (
        <p className={styles.refundNote}>
          <strong>{formatCurrency(release.refund)} simulated refund</strong>
          <span>
            Recorded {formatMalaysianDateTime(release.recordedAt)}
            {release.reason ? ` · ${release.reason}` : ""}
          </span>
        </p>
      )}
    </div>
  );
}
