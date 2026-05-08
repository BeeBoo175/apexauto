import type { ReactNode } from "react";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import {
  saleStatusLabels,
  type SaleContext,
  type SaleSnapshot,
  claimAge,
} from "@/lib/sales/contracts";
import { snapshotChanged } from "@/lib/sales/sale-history";
import { HoldWheel } from "./HoldWheel";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import styles from "./Sales.module.css";

type Pair = [label: string, value: ReactNode, changed: boolean];

// One group of facts: a lead value that names the subject, quieter supporting lines, then labelled details.
function FactGroup({
  title,
  lead,
  leadChanged = false,
  lines = [],
  pairs = [],
  children,
}: {
  title: string;
  lead: ReactNode;
  leadChanged?: boolean;
  lines?: string[];
  pairs?: Pair[];
  children?: ReactNode;
}) {
  return (
    <section className={styles.factGroup} aria-label={title}>
      <h2 className={workbench.sectionTitle}>{title}</h2>
      <p className={`${styles.factLead} ${leadChanged ? styles.changed : ""}`}>{lead}</p>
      {lines.map((line) => (
        <p key={line} className={styles.factLine}>
          {line}
        </p>
      ))}
      {children}
      {pairs.length > 0 && (
        <dl className={styles.factPairs}>
          {pairs.map(([label, value, marked]) => (
            <div key={label} className={marked ? styles.changed : undefined}>
              <dt>{label}</dt>
              <dd>{value}</dd>
            </div>
          ))}
        </dl>
      )}
    </section>
  );
}

// The car, the customer, the reservation behind the sale and who handles it, live or as a past version stood.
export function SaleFacts({
  sale,
  context,
  agent,
  decidedBy,
  now,
  previous = null,
}: {
  sale: SaleSnapshot;
  context: SaleContext;
  agent: string | null;
  decidedBy: string | null;
  now: number;
  previous?: SaleSnapshot | null;
}) {
  const changed = snapshotChanged(sale, previous);
  const running = sale.status === "Open" || sale.pausedSeconds !== null;
  const handling: Pair[] = [["Status", saleStatusLabels[sale.status], changed("status")]];
  if (sale.claimedAt) {
    handling.push([
      "Claimed",
      `${formatMalaysianDateTime(sale.claimedAt)} · ${claimAge(sale.claimedAt, now)}`,
      changed("claimedAt"),
    ]);
  }
  if (decidedBy) {
    handling.push(["Approved by", decidedBy, changed("decidedById")]);
  }
  if (sale.closedAt) {
    handling.push(["Closed", formatMalaysianDateTime(sale.closedAt), changed("closedAt")]);
  }

  return (
    <div className={styles.factGroups}>
      <FactGroup
        title="Customer"
        lead={context.customer}
        lines={[context.customerMobile, context.customerEmail].filter(Boolean)}
      />
      <FactGroup
        title="Vehicle"
        lead={context.registration}
        lines={[context.vehicle]}
        pairs={[
          ["VIN", context.vin, false],
          ["Mileage", `${context.mileageKilometres.toLocaleString("en-MY")} km`, false],
        ]}
      />
      <FactGroup
        title="Handling"
        lead={agent ?? "Not yet claimed"}
        leadChanged={changed("agentId")}
        pairs={handling}
      >
        {running && (
          <div className={changed("dueAt", "pausedSeconds") ? styles.changed : undefined}>
            <HoldWheel dueAt={sale.dueAt} pausedSeconds={sale.pausedSeconds} now={now} />
          </div>
        )}
      </FactGroup>
      <FactGroup
        title="Reservation"
        lead={context.reservationNumber}
        lines={[context.branch]}
        pairs={[
          ["Reserved", formatMalaysianDateTime(context.reservedAt), false],
          ["Deposit", formatCurrency(sale.depositCredit), false],
        ]}
      />
    </div>
  );
}
