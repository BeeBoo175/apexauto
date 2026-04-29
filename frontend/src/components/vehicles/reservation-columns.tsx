import { VehicleDescription, VehicleRegistration } from "@/components/staff/VehicleTableCells";
import { Badge } from "@/components/ui/Badge";
import type { DataColumn } from "@/components/ui/data-table/types";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { claimAge } from "@/lib/sales/sales";
import { staffColumnSizing } from "@/lib/staff/table-columns";
import {
  reservationStatuses,
  type ReservationItem,
  type ReservationView,
} from "@/lib/vehicles/reservations";
import tones from "@/components/ui/StatusTone.module.css";
import styles from "@/components/staff/StaffTable.module.css";

function When({ value }: { value: string }) {
  return (
    <time className={styles.date} dateTime={value}>
      {formatMalaysianDateTime(value)}
    </time>
  );
}

// A claimed hold's timer stands still, so the hours it kept sit beside how long ago it was taken.
function ClaimedCell({
  claim,
  now,
}: {
  claim: NonNullable<ReservationItem["claim"]>;
  now: number;
}) {
  const hours = Math.floor(claim.frozenRemainingSeconds / 3600);
  const minutes = Math.floor((claim.frozenRemainingSeconds % 3600) / 60);
  return (
    <div className={styles.inline}>
      <time className={styles.date} dateTime={claim.claimedAtUtc}>
        {claimAge(claim.claimedAtUtc, now)}
      </time>
      <span className={`${styles.muted} ${styles.context}`}>
        {hours}h {minutes}m left, paused
      </span>
    </div>
  );
}

export function reservationColumns(
  showBranch: boolean,
  view: ReservationView,
  now: number,
): DataColumn<ReservationItem>[] {
  const columns: DataColumn<ReservationItem>[] = [
    {
      id: "registration",
      label: "Registration",
      sortable: true,
      ...staffColumnSizing.registration,
      cell: (row) => <VehicleRegistration registration={row.registrationPlate} />,
    },
    {
      id: "vehicle",
      label: "Vehicle",
      sortable: true,
      ...staffColumnSizing.vehicle,
      cell: (row) => <VehicleDescription vehicle={row} />,
    },
  ];
  if (view === "history") {
    columns.push({
      id: "status",
      label: "Status",
      sortable: true,
      ...staffColumnSizing.status,
      cell: (row) => (
        <Badge className={`${tones.badge} ${tones.neutral} ${styles.status}`}>
          {reservationStatuses[row.status]}
        </Badge>
      ),
    });
  }
  columns.push(
    {
      id: "buyer",
      label: "Buyer",
      sortable: true,
      ...staffColumnSizing.person,
      cell: (row) => row.buyer,
    },
    {
      id: "total",
      label: "Reserved total",
      sortable: true,
      ...staffColumnSizing.money,
      align: "right",
      cell: (row) => <span className={styles.money}>{formatCurrency(row.total)}</span>,
    },
  );
  if (view === "handled") {
    columns.push(
      {
        id: "agent",
        label: "Handled by",
        sortable: true,
        ...staffColumnSizing.person,
        cell: (row) => row.claim?.agent,
      },
      {
        id: "claimed",
        label: "Claimed",
        sortable: true,
        ...staffColumnSizing.activity,
        cell: (row) => (row.claim ? <ClaimedCell claim={row.claim} now={now} /> : null),
      },
    );
  }
  if (view === "open") {
    columns.push({
      id: "expires",
      label: "Expires",
      sortable: true,
      ...staffColumnSizing.dateTime,
      cell: (row) => <When value={row.expiresAt} />,
    });
  }
  if (view === "history") {
    columns.push({
      id: "released",
      label: "Released",
      sortable: true,
      ...staffColumnSizing.dateTime,
      cell: (row) =>
        row.releasedAt ? (
          <When value={row.releasedAt} />
        ) : (
          <span className={styles.muted}>Not released</span>
        ),
    });
  }
  columns.push({
    id: "created",
    label: "Created",
    sortable: true,
    ...staffColumnSizing.dateTime,
    cell: (row) => <When value={row.createdAt} />,
  });
  if (showBranch) {
    columns.push({
      id: "branch",
      label: "Branch",
      sortable: true,
      ...staffColumnSizing.branch,
      cell: (row) => row.branchName,
    });
  }
  return columns;
}
