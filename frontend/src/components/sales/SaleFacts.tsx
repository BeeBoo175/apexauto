import { PersonIdentityFacts, VehicleIdentityFacts } from "@/components/staff/IdentityFacts";
import { StaffLedger } from "@/components/staff/StaffLedger";
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
import styles from "./Sales.module.css";

type Fact = [label: string, value: React.ReactNode, changed: boolean];

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
  const handling: Fact[] = [
    ["Status", saleStatusLabels[sale.status], changed("status")],
    ["Handled by", agent ?? "Nobody is handling this sale", changed("agentId")],
  ];
  if (sale.claimedAt) {
    handling.push([
      "Claimed",
      `${formatMalaysianDateTime(sale.claimedAt)} · ${claimAge(sale.claimedAt, now)}`,
      changed("claimedAt"),
    ]);
  }
  if (running) {
    handling.push([
      "Hold time",
      <HoldWheel key="wheel" dueAt={sale.dueAt} pausedSeconds={sale.pausedSeconds} now={now} />,
      changed("dueAt", "pausedSeconds"),
    ]);
  }
  if (decidedBy) {
    handling.push(["Approved by", decidedBy, changed("decidedById")]);
  }
  if (sale.closedAt) {
    handling.push(["Closed", formatMalaysianDateTime(sale.closedAt), changed("closedAt")]);
  }

  return (
    <>
      <StaffLedger title="Handling">
        {handling.map(([label, value, marked]) => (
          <div key={label} className={marked ? styles.changed : undefined}>
            <dt>{label}</dt>
            <dd>{value}</dd>
          </div>
        ))}
      </StaffLedger>
      <StaffLedger title="Customer">
        <PersonIdentityFacts
          role="Name"
          name={context.customer}
          mobile={context.customerMobile}
          email={context.customerEmail}
          linked={previous === null}
        />
      </StaffLedger>
      <StaffLedger title="Vehicle">
        <VehicleIdentityFacts
          registration={context.registration}
          vehicle={context.vehicle}
          vin={context.vin}
        />
        <div>
          <dt>Mileage</dt>
          <dd>{context.mileageKilometres.toLocaleString("en-MY")} km</dd>
        </div>
      </StaffLedger>
      <StaffLedger title="Reservation">
        <div>
          <dt>Reference</dt>
          <dd>{context.reservationNumber}</dd>
        </div>
        <div>
          <dt>Reserved</dt>
          <dd>{formatMalaysianDateTime(context.reservedAt)}</dd>
        </div>
        <div>
          <dt>Deposit</dt>
          <dd>{formatCurrency(sale.depositCredit)}</dd>
        </div>
        <div>
          <dt>Branch</dt>
          <dd>{context.branch}</dd>
        </div>
      </StaffLedger>
    </>
  );
}
