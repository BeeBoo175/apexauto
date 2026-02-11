import type { DataColumn } from "@/components/ui/data-table/types";
import type { WorklistItem } from "@/lib/staff/contracts";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { AppointmentStatus } from "./AppointmentStatus";
import styles from "./IntakeWorklist.module.css";

export function intakeColumns(
  query: string,
  onOpen: (id: string) => void,
): DataColumn<WorklistItem>[] {
  return [
    {
      id: "registrationPlate",
      label: "Registration",
      sortable: true,
      width: 170,
      minWidth: 160,
      cell: (row) => (
        <a
          className={styles.registration}
          href={`/dashboard/intake/${row.id}?${query}`}
          aria-label={`Open ${row.registrationPlate}`}
          aria-haspopup="dialog"
          onClick={(event) => {
            if (
              event.metaKey ||
              event.ctrlKey ||
              event.shiftKey ||
              event.altKey ||
              event.button !== 0
            ) {
              return;
            }
            event.preventDefault();
            onOpen(row.id);
          }}
        >
          {row.registrationPlate}
        </a>
      ),
      exportValue: (row) => row.registrationPlate,
    },
    {
      id: "customerName",
      label: "Customer",
      sortable: true,
      width: 210,
      minWidth: 160,
      cell: (row) => row.customerName,
      exportValue: (row) => row.customerName,
    },
    {
      id: "branchName",
      label: "Branch",
      sortable: true,
      width: 210,
      minWidth: 150,
      cell: (row) => row.branchName,
      exportValue: (row) => row.branchName,
    },
    {
      id: "startsAt",
      label: "Inspection time",
      exportLabel: "Inspection start (UTC)",
      sortable: true,
      width: 240,
      minWidth: 200,
      cell: (row) => <span className={styles.timing}>{formatMalaysianDateTime(row.startsAt)}</span>,
      exportValue: (row) => new Date(row.startsAt).toISOString(),
    },
    {
      id: "status",
      label: "Status",
      sortable: true,
      width: 140,
      minWidth: 120,
      cell: (row) => <AppointmentStatus status={row.status} />,
      exportValue: (row) => row.status,
    },
    {
      id: "appointment",
      label: "Appointment",
      exportLabel: "Appointment reference",
      exportOnly: true,
      exportValue: (row) => row.id,
      cell: () => null,
    },
  ];
}
