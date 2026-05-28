import type { DataColumn } from "@/components/ui/data-table/types";
import type { WorklistItem } from "@/lib/staff/contracts";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { staffColumnSizing } from "@/lib/staff/table-columns";
import { AppointmentStatus } from "./AppointmentStatus";
import { VehicleDescription, VehicleRegistration } from "./VehicleTableCells";
import styles from "./StaffTable.module.css";

export function intakeColumns(showBranch: boolean): DataColumn<WorklistItem>[] {
  const columns: DataColumn<WorklistItem>[] = [
    {
      id: "registrationPlate",
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
      ...staffColumnSizing.shortStatus,
      cell: (row) => <AppointmentStatus status={row.status} table />,
    },
    {
      id: "customerName",
      label: "Customer",
      sortable: true,
      ...staffColumnSizing.person,
      cell: (row) => row.customerName,
    },
    {
      id: "handlerEmail",
      label: "Handler",
      sortable: true,
      ...staffColumnSizing.person,
      cell: (row) => {
        if (!row.handlerId) {
          return <span className={styles.muted}>Unassigned</span>;
        }
        return (
          <span>
            {row.handlerEmail}
            {!row.handlerEligible && <span className={styles.muted}> (Ineligible)</span>}
          </span>
        );
      },
    },
    {
      id: "startsAt",
      label: "Inspection time",
      sortable: true,
      ...staffColumnSizing.dateTime,
      cell: (row) => (
        <time className={styles.date} dateTime={row.startsAt}>
          {formatMalaysianDateTime(row.startsAt)}
        </time>
      ),
    },
  ];
  if (showBranch) {
    columns.push({
      id: "branchName",
      label: "Branch",
      sortable: true,
      ...staffColumnSizing.branch,
      cell: (row) => row.branchName,
    });
  }
  return columns;
}
