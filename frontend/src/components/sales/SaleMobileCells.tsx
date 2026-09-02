import { CustomerAvatar } from "@/components/staff/CustomerAvatar";
import { SuspendedPerson } from "@/components/staff/SuspensionBadge";
import { VehicleRegistration } from "@/components/staff/VehicleTableCells";
import { Badge } from "@/components/ui/Badge";
import { HoldTimer } from "@/components/staff/HoldTimer";
import { formatCurrency } from "@/lib/currency-format";
import {
  saleDiscountOverLimit,
  saleHasHoldTimer,
  saleReference,
  type SaleSummary,
} from "@/lib/sales/contracts";
import { SaleStatus } from "./SaleStatus";
import styles from "./SalesMobile.module.css";
import tones from "@/components/ui/StatusTone.module.css";
import tableStyles from "@/components/staff/StaffTable.module.css";

import type { SaleMetric } from "./sale-mobile";

export function SaleMobileIdentity({ row }: { row: SaleSummary }) {
  const vehicle = `${row.manufactureYear} ${row.make} ${row.model} ${row.trim}`;
  return (
    <div className={styles.identity}>
      <VehicleRegistration registration={row.registration} />
      <strong className={styles.vehicle} title={vehicle}>
        {vehicle}
      </strong>
      <span className={styles.customer}>
        <SuspendedPerson person="Customer" suspended={row.customerSuspended}>
          <CustomerAvatar id={row.customerId} name={row.customer} />
        </SuspendedPerson>
      </span>
      <span className={styles.meta}>{saleReference(row)}</span>
    </div>
  );
}

export function SaleMobileMetric({
  row,
  metric,
  now,
  showBranch,
}: {
  row: SaleSummary;
  metric: SaleMetric;
  now: number;
  showBranch: boolean;
}) {
  const deposit = row.status === "AwaitingDeposit";
  const overLimit = !deposit && saleDiscountOverLimit(row);
  let value = <SaleStatus status={row.status} table />;
  if (metric === "balance" || metric === "discount") {
    value = deposit ? (
      <span className={styles.meta}>Not yet agreed</span>
    ) : (
      <strong className={styles.money}>
        {formatCurrency(metric === "balance" ? row.balancePayable : row.negotiatedDiscount)}
      </strong>
    );
  }
  if (metric === "timeLeft") {
    value = saleHasHoldTimer(row.status) ? (
      <HoldTimer
        dueAt={row.dueAt}
        pausedSeconds={row.pausedSeconds}
        totalSeconds={row.totalHoldSeconds}
        now={now}
        compact
      />
    ) : (
      <span className={styles.meta}>Hold ended</span>
    );
  }
  return (
    <div className={styles.metric}>
      {value}
      {metric !== "status" && <SaleStatus status={row.status} table />}
      {overLimit && (
        <Badge className={`${tones.badge} ${tones.danger} ${tableStyles.status}`}>Over limit</Badge>
      )}
      {metric === "status" && (
        <span className={styles.meta}>{row.agentName || row.agent || "Unclaimed"}</span>
      )}
      {showBranch && <span className={styles.meta}>{row.branch}</span>}
    </div>
  );
}
