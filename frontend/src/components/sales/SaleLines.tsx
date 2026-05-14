import { formatCurrency } from "@/lib/currency-format";
import type { SaleSnapshot } from "@/lib/sales/contracts";
import styles from "./Sales.module.css";

type Line = { label: string; value: string; note?: string };

const credit = (amount: number) => `− ${formatCurrency(amount)}`;

// Confirmed pricing uses the same presentation for current and historical records.
export function SaleLines({ sale }: { sale: SaleSnapshot }) {
  const limit = sale.discountLimitAtSubmit;
  const excess =
    limit !== null && sale.negotiatedDiscount > limit ? sale.negotiatedDiscount - limit : 0;
  let limitNote: string | undefined;
  if (limit !== null) {
    limitNote =
      excess > 0
        ? `${formatCurrency(excess)} over the ${formatCurrency(limit)} agent limit`
        : `Within the ${formatCurrency(limit)} agent limit`;
  }
  const groups: { title: string; lines: Line[] }[] = [
    {
      title: "Car",
      lines: [
        { label: "Vehicle price", value: formatCurrency(sale.basePrice) },
        {
          label: sale.promoCode ? `Promo ${sale.promoCode}` : "Promo discount",
          value: credit(sale.promoDiscount),
        },
        {
          label: "Negotiated discount",
          value: credit(sale.negotiatedDiscount),
          note: limitNote,
        },
      ],
    },
    {
      title: "Fees and tax",
      lines: [
        {
          label: "Processing fee",
          value: formatCurrency(sale.processingFee),
        },
        { label: "Road tax", value: formatCurrency(sale.roadTax) },
        {
          label: `Service tax (${Number((sale.sstRate * 100).toFixed(2))}% of fee)`,
          value: formatCurrency(sale.serviceTax),
        },
      ],
    },
    {
      title: "Extras and credits",
      lines: [
        {
          label: sale.warrantyName ?? "No warranty",
          value: formatCurrency(sale.warrantyPrice),
        },
        { label: "Reservation deposit", value: credit(sale.depositCredit) },
        {
          label: sale.tradeInLabel ? `Trade-in ${sale.tradeInLabel}` : "No trade-in",
          value: credit(sale.tradeInAllowance),
        },
      ],
    },
  ];
  return (
    <section className={styles.price} aria-label="Price">
      {groups.map((group) => (
        <dl key={group.title} className={styles.priceLines} aria-label={group.title}>
          {group.lines.map((line) => (
            <div key={line.label}>
              <dt>
                {line.label}
                {line.note && (
                  <span className={styles.priceNote} data-over={excess > 0 || undefined}>
                    {line.note}
                  </span>
                )}
              </dt>
              <dd>{line.value}</dd>
            </div>
          ))}
        </dl>
      ))}
      <dl className={styles.priceTotal}>
        <div>
          <dt>Balance payable</dt>
          <dd>{formatCurrency(sale.balancePayable)}</dd>
        </div>
      </dl>
    </section>
  );
}
