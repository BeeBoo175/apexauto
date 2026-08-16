import { CustomerAvatar } from "@/components/staff/CustomerAvatar";
import { HandlerAvatar } from "@/components/staff/HandlerAvatar";
import { SuspendedPerson } from "@/components/staff/SuspensionBadge";
import { VehicleDescription, VehicleRegistration } from "@/components/staff/VehicleTableCells";
import { Badge } from "@/components/ui/Badge";
import type { DataColumn } from "@/components/ui/data-table/types";
import type { CurrentActor } from "@/lib/auth/actor";
import { formatCurrency } from "@/lib/currency-format";
import {
  saleDiscountOverLimit,
  saleHasHoldTimer,
  saleReference,
  type SaleSummary,
} from "@/lib/sales/contracts";
import { staffColumnSizing } from "@/lib/staff/table-columns";
import { DEPOSIT_REQUEST_HOURS } from "@/lib/vehicles/deposit-request";
import { HoldTimer } from "@/components/staff/HoldTimer";
import { SaleStatus } from "./SaleStatus";
import tones from "@/components/ui/StatusTone.module.css";
import styles from "@/components/staff/StaffTable.module.css";

function Discount({ row }: { row: SaleSummary }) {
  if (row.status === "AwaitingDeposit") {
    return <span className={styles.muted}>—</span>;
  }
  return (
    <div className={styles.inline}>
      <span className={styles.money}>{formatCurrency(row.negotiatedDiscount)}</span>
      {saleDiscountOverLimit(row) && (
        <Badge className={`${tones.badge} ${tones.warning} ${styles.status}`}>Over limit</Badge>
      )}
    </div>
  );
}

// Every manager queue shares one column set so values keep their position while reviewing work.
export function saleColumns(role: CurrentActor["role"], now: number): DataColumn<SaleSummary>[] {
  const columns: DataColumn<SaleSummary>[] = [
    {
      id: "reference",
      label: "Reference",
      sortable: true,
      width: 216,
      cell: (row) => <strong>{saleReference(row)}</strong>,
    },
    {
      id: "registration",
      label: "Registration",
      sortable: true,
      ...staffColumnSizing.registration,
      cell: (row) => <VehicleRegistration registration={row.registration} />,
    },
    {
      id: "vehicle",
      label: "Vehicle",
      sortable: true,
      ...staffColumnSizing.vehicle,
      cell: (row) => <VehicleDescription vehicle={row} />,
    },
    {
      id: "customer",
      label: "Customer",
      sortable: true,
      ...staffColumnSizing.person,
      cell: (row) => (
        <SuspendedPerson person="Customer" suspended={row.customerSuspended}>
          <CustomerAvatar id={row.customerId} name={row.customer} />
        </SuspendedPerson>
      ),
    },
    {
      id: "agent",
      label: "Agent",
      sortable: true,
      ...staffColumnSizing.person,
      cell: (row) =>
        row.agentId && row.agent ? (
          <SuspendedPerson person="Handler" suspended={row.agentSuspended}>
            <HandlerAvatar
              id={row.agentId}
              identity={row.agent}
              name={row.agentName}
              pictureId={row.agentPictureId}
            />
          </SuspendedPerson>
        ) : (
          <span className={styles.muted}>Unclaimed</span>
        ),
    },
    {
      id: "status",
      label: "Status",
      sortable: true,
      ...staffColumnSizing.status,
      cell: (row) => <SaleStatus status={row.status} table />,
    },
    {
      id: "discount",
      label: "Negotiated discount",
      sortable: true,
      ...staffColumnSizing.money,
      align: "right",
      cell: (row) => <Discount row={row} />,
    },
    {
      id: "balance",
      label: "Balance payable",
      sortable: true,
      ...staffColumnSizing.money,
      align: "right",
      cell: (row) =>
        row.status === "AwaitingDeposit" ? (
          <span className={styles.muted}>—</span>
        ) : (
          <span className={styles.money}>{formatCurrency(row.balancePayable)}</span>
        ),
    },
    {
      id: "timeLeft",
      label: "Time left",
      sortable: true,
      ...staffColumnSizing.shortStatus,
      cell: (row) =>
        !saleHasHoldTimer(row.status) ? (
          <span className={styles.muted}>—</span>
        ) : (
          <HoldTimer
            dueAt={row.dueAt}
            pausedSeconds={row.pausedSeconds}
            now={now}
            totalSeconds={
              row.status === "AwaitingDeposit" ? DEPOSIT_REQUEST_HOURS * 3600 : undefined
            }
            compact
          />
        ),
    },
  ];
  if (role === "GeneralManager") {
    columns.push({
      id: "branch",
      label: "Branch",
      sortable: true,
      ...staffColumnSizing.branch,
      cell: (row) => row.branch,
    });
  }
  return columns;
}
