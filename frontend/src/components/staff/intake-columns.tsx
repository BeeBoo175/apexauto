import type { DataColumn } from "@/components/ui/data-table/types";
import type { WorklistItem } from "@/lib/staff/contracts";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { staffColumnSizing } from "@/lib/staff/table-columns";
import { AppointmentStatus } from "./AppointmentStatus";
import { HandlerAvatar } from "./HandlerAvatar";
import { SuspensionBadge } from "./SuspensionBadge";
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
      cell: (row) =>
        row.customerSuspended ? (
          <span className={styles.handler}>
            {row.customerName}
            <SuspensionBadge person="Customer" />
          </span>
        ) : (
          row.customerName
        ),
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
          <span className={styles.handler}>
            <HandlerAvatar
              id={row.handlerId}
              identity={row.handlerEmail ?? ""}
              name={row.handlerName}
              pictureId={row.handlerPictureId}
            />
            {row.handlerSuspended && <SuspensionBadge person="Handler" />}
            {!row.handlerEligible && !row.handlerSuspended && (
              <span className={styles.muted}> (Ineligible)</span>
            )}
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
