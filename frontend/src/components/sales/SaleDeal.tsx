import type { ReactNode } from "react";
import { formatCurrency } from "@/lib/currency-format";
import type { SaleContext, SaleSnapshot } from "@/lib/sales/contracts";
import { SaleLines } from "./SaleLines";
import { SaleCompletionEvidence } from "./SaleCompletionEvidence";
import record from "@/components/staff/StaffRecordContent.module.css";
import styles from "./Sales.module.css";

// Current, edited and retained terms share one price/detail composition.
export function SaleDeal({
  sale,
  context,
  children,
}: {
  sale: SaleSnapshot;
  context: SaleContext;
  children?: ReactNode;
}) {
  return (
    <div className={styles.deal}>
      <div className={record.group}>
        <div className={record.group}>
          <span className={record.context}>{context.registration}</span>
          <h3 className={record.title}>{context.vehicle}</h3>
        </div>
      </div>
      <div className={styles.dealColumns}>
        <div className={styles.terms}>
          {children ?? (
            <section className={record.group} aria-label="Agreed terms">
              <h4>Agreed terms</h4>
              <dl className={record.facts}>
                <div>
                  <dt>Warranty</dt>
                  <dd>{sale.warrantyName ?? "No warranty"}</dd>
                </div>
                <div>
                  <dt>Trade-in</dt>
                  <dd>{sale.tradeInLabel ?? "No trade-in"}</dd>
                </div>
                {sale.discountLimitAtSubmit !== null && (
                  <div>
                    <dt>Discount limit</dt>
                    <dd>{formatCurrency(sale.discountLimitAtSubmit)}</dd>
                  </div>
                )}
              </dl>
            </section>
          )}
        </div>
        <SaleLines sale={sale} />
      </div>
      <SaleCompletionEvidence sale={sale} />
    </div>
  );
}
