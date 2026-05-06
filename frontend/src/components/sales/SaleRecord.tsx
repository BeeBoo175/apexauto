import { Badge } from "@/components/ui/Badge";
import type { useSaleEditor } from "@/hooks/use-sale-editor";
import { formatCurrency } from "@/lib/currency-format";
import { SaleStatus } from "./SaleStatus";
import workbench from "@/components/staff/StaffWorkspace.module.css";

// What the sale is worth and where it stands, then any newer version, unconfirmed command or failed read.
export function SaleRecord({ editor }: { editor: ReturnType<typeof useSaleEditor> }) {
  const { sale, context } = editor.snapshot;
  const overLimit =
    sale.discountLimitAtSubmit !== null && sale.negotiatedDiscount > sale.discountLimitAtSubmit;
  return (
    <section className={workbench.summary} aria-label="Sale balance and status">
      <div className={workbench.summaryMain}>
        <div>
          <span className={workbench.summaryLabel}>Balance payable</span>
          <strong className={workbench.summaryValue}>{formatCurrency(sale.balancePayable)}</strong>
        </div>
        <div className={workbench.summaryContext}>
          <SaleStatus status={sale.status} />
          {overLimit && <Badge variant="warning">Discount over limit</Badge>}
          <span className={workbench.reference}>
            {sale.orderNumber ?? context.reservationNumber}
          </span>
        </div>
      </div>
      {editor.stale && (
        <p role="status">
          A newer version of this sale is available. Your terms remain retained; use the latest
          values before saving again.
        </p>
      )}
      {editor.confirmed && (
        <p role="alert">
          The action was confirmed, but the latest sale could not be read. Refresh before another
          action.
        </p>
      )}
      {editor.uncertain && (
        <p role="alert">
          The last action is unconfirmed. Repeat it to reconcile the same operation.
        </p>
      )}
    </section>
  );
}
