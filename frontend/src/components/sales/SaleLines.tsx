import { StaffLedger } from "@/components/staff/StaffLedger";
import { formatCurrency } from "@/lib/currency-format";
import type { SaleSnapshot } from "@/lib/sales/sale";
import { snapshotChanged } from "@/lib/sales/sale-history";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import styles from "./Sales.module.css";

// The itemized price a sale carries, live or as it stood, down to the balance payable. Fields a past version
// changed are marked against the version before it.
export function SaleLines({
  sale,
  previous = null,
}: {
  sale: SaleSnapshot;
  previous?: SaleSnapshot | null;
}) {
  const changed = snapshotChanged(sale, previous);
  const lines: [string, string, boolean][] = [
    ["Vehicle price", formatCurrency(sale.basePrice), false],
    [
      sale.promoCode ? `Promo ${sale.promoCode}` : "Promo discount",
      `− ${formatCurrency(sale.promoDiscount)}`,
      false,
    ],
    [
      "Negotiated discount",
      `− ${formatCurrency(sale.negotiatedDiscount)}`,
      changed("negotiatedDiscount"),
    ],
    ["Processing fee", formatCurrency(sale.processingFee), changed("processingFee")],
    ["Road tax", formatCurrency(sale.roadTax), false],
    [
      `Service tax (${Number((sale.sstRate * 100).toFixed(2))}% of fee)`,
      formatCurrency(sale.serviceTax),
      changed("serviceTax"),
    ],
    [
      sale.warrantyName ?? "Warranty",
      formatCurrency(sale.warrantyPrice),
      changed("warrantyPackageId"),
    ],
    ["Deposit credit", `− ${formatCurrency(sale.depositCredit)}`, false],
    [
      sale.tradeInLabel ? `Trade-in ${sale.tradeInLabel}` : "Trade-in allowance",
      `− ${formatCurrency(sale.tradeInAllowance)}`,
      changed("tradeInAppointmentId", "tradeInAllowance"),
    ],
  ];
  return (
    <StaffLedger title="Price">
      {lines.map(([label, value, marked]) => (
        <div key={label} className={marked ? styles.changed : undefined}>
          <dt>{label}</dt>
          <dd>{value}</dd>
        </div>
      ))}
      <div className={`${workbench.total} ${changed("balancePayable") ? styles.changed : ""}`}>
        <dt>Balance payable</dt>
        <dd>{formatCurrency(sale.balancePayable)}</dd>
      </div>
    </StaffLedger>
  );
}
