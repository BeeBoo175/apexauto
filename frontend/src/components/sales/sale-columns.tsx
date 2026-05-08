import { VehicleDescription, VehicleRegistration } from "@/components/staff/VehicleTableCells";
import type { DataColumn } from "@/components/ui/data-table/types";
import type { CurrentActor } from "@/lib/auth/actor";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { claimAge, saleReference, type SaleSummary, type TableView } from "@/lib/sales/sales";
import { staffColumnSizing } from "@/lib/staff/table-columns";
import { HoldWheel } from "./HoldWheel";
import { SaleStatus } from "./SaleStatus";
import styles from "@/components/staff/StaffTable.module.css";

function When({ value }: { value: string | null }) {
  if (!value) {
    return <span className={styles.muted}>—</span>;
  }
  return (
    <time className={styles.date} dateTime={value}>
      {formatMalaysianDateTime(value)}
    </time>
  );
}

const over = (row: SaleSummary) =>
  row.discountLimitAtSubmit !== null && row.negotiatedDiscount > row.discountLimitAtSubmit
    ? row.negotiatedDiscount - row.discountLimitAtSubmit
    : 0;

// The columns every view shares, then what each view is for: the excess to approve, the time left, how long a
// claim has stood, what was approved, or how the sale ended.
export function saleColumns(
  role: CurrentActor["role"],
  view: TableView,
  now: number,
): DataColumn<SaleSummary>[] {
  const columns: DataColumn<SaleSummary>[] = [
    {
      id: "reference",
      label: "Reference",
      sortable: true,
      // Wide enough for "VPO-2026-000001" beside its sort and filter controls.
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
      cell: (row) => row.customer,
    },
    {
      id: "agent",
      label: "Agent",
      sortable: true,
      ...staffColumnSizing.person,
      cell: (row) => row.agent ?? <span className={styles.muted}>Unclaimed</span>,
    },
    {
      id: "status",
      label: "Status",
      // Only History mixes statuses; every other tab holds one.
      sortable: view === "History",
      ...staffColumnSizing.status,
      cell: (row) => <SaleStatus status={row.status} table />,
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
  switch (view) {
    case "NeedsApproval":
      columns.push(
        {
          id: "discount",
          label: "Negotiated discount",
          sortable: true,
          ...staffColumnSizing.money,
          align: "right",
          cell: (row) => (
            <span className={styles.money}>{formatCurrency(row.negotiatedDiscount)}</span>
          ),
        },
        {
          id: "overLimit",
          label: "Over limit by",
          sortable: true,
          ...staffColumnSizing.money,
          align: "right",
          cell: (row) => <span className={styles.money}>{formatCurrency(over(row))}</span>,
        },
        {
          id: "submitted",
          label: "Submitted",
          sortable: true,
          ...staffColumnSizing.dateTime,
          cell: (row) => <When value={row.submittedAt} />,
        },
      );
      break;
    case "Open":
      columns.push({
        id: "due",
        label: "Time left",
        sortable: true,
        ...staffColumnSizing.statusWithContext,
        cell: (row) => <HoldWheel dueAt={row.dueAt} pausedSeconds={null} now={now} />,
      });
      break;
    case "BeingHandled":
      columns.push({
        id: "claimed",
        label: "Claimed",
        sortable: true,
        ...staffColumnSizing.statusWithContext,
        cell: (row) => (
          <div className={styles.inline}>
            <span>{claimAge(row.claimedAt, now)}</span>
            <HoldWheel dueAt={null} pausedSeconds={row.pausedSeconds} now={now} />
          </div>
        ),
      });
      break;
    case "Approved":
      columns.push(
        {
          id: "balance",
          label: "Balance payable",
          sortable: true,
          ...staffColumnSizing.money,
          align: "right",
          cell: (row) => <span className={styles.money}>{formatCurrency(row.balancePayable)}</span>,
        },
        {
          id: "decided",
          label: "Approved",
          sortable: true,
          ...staffColumnSizing.dateTime,
          cell: (row) => <When value={row.decidedAt} />,
        },
      );
      break;
    default:
      columns.push({
        id: "closed",
        label: "Closed",
        sortable: true,
        ...staffColumnSizing.dateTime,
        cell: (row) => <When value={row.closedAt} />,
      });
  }
  return columns;
}
