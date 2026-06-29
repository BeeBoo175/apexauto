import { HandlerAvatar } from "@/components/staff/HandlerAvatar";
import { SuspensionBadge } from "@/components/staff/SuspensionBadge";
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
import { HoldWheel } from "./HoldWheel";
import { SaleStatus } from "./SaleStatus";
import tones from "@/components/ui/StatusTone.module.css";
import styles from "@/components/staff/StaffTable.module.css";

function Discount({ row }: { row: SaleSummary }) {
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
      cell: (row) =>
        row.customerSuspended ? (
          <>
            {row.customer} <SuspensionBadge person="Customer" />
          </>
        ) : (
          row.customer
        ),
    },
    {
      id: "agent",
      label: "Agent",
      sortable: true,
      ...staffColumnSizing.person,
      cell: (row) =>
        row.agentId && row.agent ? (
          <>
            <HandlerAvatar
              id={row.agentId}
              identity={row.agent}
              name={row.agentName}
              pictureId={row.agentPictureId}
            />
            {row.agentSuspended && <SuspensionBadge person="Handler" />}
          </>
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
      cell: (row) => <span className={styles.money}>{formatCurrency(row.balancePayable)}</span>,
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
          <HoldWheel dueAt={row.dueAt} pausedSeconds={row.pausedSeconds} now={now} compact />
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
