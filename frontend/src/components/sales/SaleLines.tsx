import { formatCurrency } from "@/lib/currency-format";
import type { SaleSnapshot } from "@/lib/sales/contracts";
import { snapshotChanged } from "@/lib/sales/sale-history";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import styles from "./Sales.module.css";

type Line = { label: string; value: string; changed: boolean; note?: string };

const credit = (amount: number) => `− ${formatCurrency(amount)}`;

// The itemized price a sale carries, live or as it stood, grouped as the customer reads it: what the car costs
// them, what the paperwork adds, and what is extra or already paid, down to the balance payable. Fields a past
// version changed are marked against the version before it.
export function SaleLines({
  sale,
  previous = null,
}: {
  sale: SaleSnapshot;
  previous?: SaleSnapshot | null;
}) {
  const changed = snapshotChanged(sale, previous);
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
        { label: "Vehicle price", value: formatCurrency(sale.basePrice), changed: false },
        {
          label: sale.promoCode ? `Promo ${sale.promoCode}` : "Promo discount",
          value: credit(sale.promoDiscount),
          changed: false,
        },
        {
          label: "Negotiated discount",
          value: credit(sale.negotiatedDiscount),
          changed: changed("negotiatedDiscount"),
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
          changed: changed("processingFee"),
        },
        { label: "Road tax", value: formatCurrency(sale.roadTax), changed: false },
        {
          label: `Service tax (${Number((sale.sstRate * 100).toFixed(2))}% of fee)`,
          value: formatCurrency(sale.serviceTax),
          changed: changed("serviceTax"),
        },
      ],
    },
    {
      title: "Extras and credits",
      lines: [
        {
          label: sale.warrantyName ?? "No warranty",
          value: formatCurrency(sale.warrantyPrice),
          changed: changed("warrantyPackageId"),
        },
        { label: "Reservation deposit", value: credit(sale.depositCredit), changed: false },
        {
          label: sale.tradeInLabel ? `Trade-in ${sale.tradeInLabel}` : "No trade-in",
          value: credit(sale.tradeInAllowance),
          changed: changed("tradeInAppointmentId", "tradeInAllowance"),
        },
      ],
    },
  ];
  return (
    <section className={styles.price} aria-label="Price">
      {groups.map((group) => (
        <section key={group.title} className={styles.priceGroup} aria-label={group.title}>
          <h2 className={workbench.sectionTitle}>{group.title}</h2>
          <dl className={styles.priceLines}>
            {group.lines.map((line) => (
              <div key={line.label} className={line.changed ? styles.changed : undefined}>
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
        </section>
      ))}
      <dl className={`${styles.priceTotal} ${changed("balancePayable") ? styles.changed : ""}`}>
        <dt>Balance payable</dt>
        <dd>{formatCurrency(sale.balancePayable)}</dd>
      </dl>
    </section>
  );
}
