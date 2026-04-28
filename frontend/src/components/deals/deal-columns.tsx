import { VehicleDescription, VehicleRegistration } from "@/components/staff/VehicleTableCells";
import { Badge } from "@/components/ui/Badge";
import type { DataColumn } from "@/components/ui/data-table/types";
import type { CurrentActor } from "@/lib/auth/actor";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { dealStatusLabels, type DealStatus, type DealSummary } from "@/lib/deals/deals";
import { staffColumnSizing } from "@/lib/staff/table-columns";
import tones from "@/components/ui/StatusTone.module.css";
import styles from "@/components/staff/StaffTable.module.css";

const statusTones: Record<DealStatus, string> = {
  Draft: tones.info,
  PendingManagerApproval: tones.warning,
  Approved: tones.success,
  Cancelled: tones.neutral,
};

function When({ value }: { value: string }) {
  return (
    <time className={styles.date} dateTime={value}>
      {formatMalaysianDateTime(value)}
    </time>
  );
}

// Only open deals have something left to do, so only they carry the next step beside the status.
function StatusCell({ row }: { row: DealSummary }) {
  const open = row.status === "Draft" || row.status === "PendingManagerApproval";
  return (
    <div className={styles.inline}>
      <Badge className={`${tones.badge} ${statusTones[row.status]} ${styles.status}`}>
        {dealStatusLabels[row.status]}
      </Badge>
      {open && (
        <span className={`${styles.muted} ${styles.context}`} title={row.nextStep}>
          {row.nextStep}
        </span>
      )}
    </div>
  );
}

// The excess over the limit captured at submit is what a manager weighs, so it sits beside the amount.
function Discount({ row }: { row: DealSummary }) {
  if (row.negotiatedDiscount === 0) {
    return <span className={styles.muted}>None</span>;
  }
  const over =
    row.discountLimitAtSubmit !== null && row.negotiatedDiscount > row.discountLimitAtSubmit
      ? row.negotiatedDiscount - row.discountLimitAtSubmit
      : 0;
  return (
    <span className={styles.money}>
      {formatCurrency(row.negotiatedDiscount)}
      {over > 0 && ` · ${formatCurrency(over)} over limit`}
    </span>
  );
}

export function dealColumns(role: CurrentActor["role"]): DataColumn<DealSummary>[] {
  const manager = role === "BranchManager" || role === "GeneralManager";
  const columns: DataColumn<DealSummary>[] = [
    {
      id: "number",
      label: "Deal",
      sortable: true,
      // Wide enough for "VPO-2026-000001" beside its sort and filter controls.
      width: 216,
      cell: (row) =>
        row.orderNumber ? (
          <strong>{row.orderNumber}</strong>
        ) : (
          <span className={styles.muted}>Not submitted</span>
        ),
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
      cell: (row) => row.customer,
    },
    {
      id: "status",
      label: "Status",
      sortable: true,
      ...staffColumnSizing.statusWithContext,
      cell: (row) => <StatusCell row={row} />,
    },
    {
      id: "balance",
      label: "Balance payable",
      sortable: true,
      ...staffColumnSizing.money,
      align: "right",
      cell: (row) => <span className={styles.money}>{formatCurrency(row.balancePayable)}</span>,
    },
  ];
  if (manager) {
    columns.push(
      {
        id: "discount",
        label: "Negotiated discount",
        sortable: true,
        width: 232,
        align: "right",
        cell: (row) => <Discount row={row} />,
      },
      {
        id: "agent",
        label: "Agent",
        sortable: true,
        ...staffColumnSizing.person,
        cell: (row) => row.agent,
      },
    );
  }
  if (role === "GeneralManager") {
    columns.push({
      id: "branch",
      label: "Branch",
      ...staffColumnSizing.branch,
      cell: (row) => row.branch,
    });
  }
  columns.push({
    id: "updated",
    label: "Updated",
    sortable: true,
    ...staffColumnSizing.dateTime,
    cell: (row) => <When value={row.updatedAtUtc} />,
  });
  return columns;
}
