import { VehicleDescription, VehicleRegistration } from "@/components/staff/VehicleTableCells";
import { Badge } from "@/components/ui/Badge";
import type { DataColumn } from "@/components/ui/data-table/types";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { staffColumnSizing } from "@/lib/staff/table-columns";
import { reservationStatuses, type ReservationItem } from "@/lib/vehicles/reservations";
import tones from "@/components/ui/StatusTone.module.css";
import styles from "@/components/staff/StaffTable.module.css";

export function reservationColumns(
  showBranch: boolean,
  isActive: boolean,
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
    {
      id: "status",
      label: "Status",
      sortable: true,
      ...staffColumnSizing.status,
      cell: (row) => (
        <Badge
          className={`${tones.badge} ${row.status === "Active" ? tones.warning : tones.neutral} ${styles.status}`}
        >
          {reservationStatuses[row.status]}
        </Badge>
      ),
    },
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
    {
      id: isActive ? "expires" : "released",
      label: isActive ? "Expires" : "Released",
      sortable: true,
      ...staffColumnSizing.dateTime,
      cell: (row) => {
        const at = isActive ? row.expiresAt : row.releasedAt;
        if (!at) {
          return <span className={styles.muted}>Not released</span>;
        }
        return (
          <time className={styles.date} dateTime={at}>
            {formatMalaysianDateTime(at)}
          </time>
        );
      },
    },
    {
      id: "created",
      label: "Created",
      sortable: true,
      ...staffColumnSizing.dateTime,
      cell: (row) => (
        <time className={styles.date} dateTime={row.createdAt}>
          {formatMalaysianDateTime(row.createdAt)}
        </time>
      ),
    },
  ];
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
