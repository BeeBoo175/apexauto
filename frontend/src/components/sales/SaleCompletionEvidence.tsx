import type { SaleSnapshot } from "@/lib/sales/contracts";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import record from "@/components/staff/StaffRecordContent.module.css";
import styles from "./SaleCompletion.module.css";

const methodLabels = {
  Cash: "Cash",
  BankTransfer: "Bank transfer",
  HirePurchase: "Hire purchase",
  ZeroBalance: "Zero balance confirmed",
};

export function SaleCompletionEvidence({ sale }: { sale: SaleSnapshot }) {
  const payment = sale.paymentEvidence;
  const handover = sale.handoverEvidence;
  if (!payment && !handover) {
    return null;
  }
  return (
    <section className={styles.evidence} aria-label="Settlement and delivery evidence">
      <div className={styles.columns}>
        {payment && (
          <section className={styles.group} aria-label="Payment evidence">
            <h4 className={record.label}>Payment received</h4>
            <dl className={record.facts}>
              {sale.settledAt && (
                <div>
                  <dt>Settled</dt>
                  <dd>{formatMalaysianDateTime(sale.settledAt)}</dd>
                </div>
              )}
              <div>
                <dt>Method</dt>
                <dd>{methodLabels[payment.method]}</dd>
              </div>
              <div>
                <dt>Received</dt>
                <dd>{formatCurrency(payment.receivedAmount)}</dd>
              </div>
              {payment.paymentReference && (
                <div>
                  <dt>Payment reference</dt>
                  <dd>{payment.paymentReference}</dd>
                </div>
              )}
              {payment.paymentDate && (
                <div>
                  <dt>Payment date</dt>
                  <dd>{payment.paymentDate}</dd>
                </div>
              )}
              {payment.bank && (
                <div>
                  <dt>Financing bank</dt>
                  <dd>{payment.bank}</dd>
                </div>
              )}
              {payment.method === "HirePurchase" && (
                <div>
                  <dt>Financing</dt>
                  <dd>{formatCurrency(payment.financingAmount)}</dd>
                </div>
              )}
              {payment.louReference && (
                <div>
                  <dt>Verified LOU</dt>
                  <dd>{payment.louReference}</dd>
                </div>
              )}
            </dl>
          </section>
        )}
        {handover && (
          <section className={styles.group} aria-label="Handover evidence">
            <h4 className={record.label}>Vehicle delivered</h4>
            <dl className={record.facts}>
              {sale.deliveredAt && (
                <div>
                  <dt>Delivered</dt>
                  <dd>{formatMalaysianDateTime(sale.deliveredAt)}</dd>
                </div>
              )}
              <div>
                <dt>Final odometer</dt>
                <dd>{handover.odometerKilometres.toLocaleString("en-MY")} km</dd>
              </div>
              <div>
                <dt>Keys delivered</dt>
                <dd>{handover.keysDelivered}</dd>
              </div>
              <div>
                <dt>Customer receipt</dt>
                <dd>{handover.customerAcknowledged ? "Acknowledged" : "Not acknowledged"}</dd>
              </div>
              <div>
                <dt>Ownership transfer</dt>
                <dd>{handover.ownershipTransferCompleted ? "Complete" : "Not confirmed"}</dd>
              </div>
              <div>
                <dt>Insurance</dt>
                <dd>{handover.insuranceConfirmed ? "Confirmed" : "Not confirmed"}</dd>
              </div>
            </dl>
          </section>
        )}
      </div>
    </section>
  );
}
