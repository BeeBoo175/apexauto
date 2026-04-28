import { formatCurrency } from "@/lib/currency-format";
import type { Deal } from "@/lib/deals/deals";
import workbench from "@/components/staff/StaffWorkspace.module.css";

// The itemized price a deal currently carries, down to the balance payable.
export function DealLines({ deal }: { deal: Deal }) {
  const lines: [string, string][] = [
    ["Vehicle price", formatCurrency(deal.basePrice)],
    [
      deal.promoCode ? `Promo ${deal.promoCode}` : "Promo discount",
      `− ${formatCurrency(deal.promoDiscount)}`,
    ],
    ["Negotiated discount", `− ${formatCurrency(deal.negotiatedDiscount)}`],
    ["Processing fee", formatCurrency(deal.processingFee)],
    ["Road tax", formatCurrency(deal.roadTax)],
    [
      `Service tax (${Number((deal.sstRate * 100).toFixed(2))}% of fee)`,
      formatCurrency(deal.serviceTax),
    ],
    [deal.warrantyName ?? "Warranty", formatCurrency(deal.warrantyPrice)],
    ["Deposit credit", `− ${formatCurrency(deal.depositCredit)}`],
    ["Trade-in allowance", `− ${formatCurrency(deal.tradeInAllowance)}`],
    ["Balance payable", formatCurrency(deal.balancePayable)],
  ];
  return (
    <dl className={workbench.details}>
      {lines.map(([label, value]) => (
        <div key={label}>
          <dt>{label}</dt>
          <dd>{value}</dd>
        </div>
      ))}
    </dl>
  );
}
