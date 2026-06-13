import type { ReactNode } from "react";
import { Button } from "@/components/ui/Button";
import { formatMalaysianDateTime } from "@/lib/date-format";
import styles from "./StaffQueueWorkspace.module.css";

interface QueueVehicleRow {
  registrationPlate: string;
  manufactureYear: number;
  make: string;
  model: string;
  trim: string;
  customerName: string;
  startsAt: string;
}

export function StaffQueueVehicleRow({
  row,
  status,
  selected,
  pending,
  onOpen,
}: {
  row: QueueVehicleRow;
  status: ReactNode;
  selected: boolean;
  pending: boolean;
  onOpen: () => void;
}) {
  return (
    <li>
      <Button
        variant="ghost"
        size="row"
        className={styles.job}
        aria-pressed={selected}
        aria-busy={pending || undefined}
        onClick={onOpen}
      >
        <span className={styles.jobHeading}>
          <span className={styles.identity}>
            <strong>{row.registrationPlate}</strong>
            <span>
              {row.manufactureYear} {row.make} {row.model} {row.trim}
            </span>
          </span>
          {status}
        </span>
        <span className={styles.jobMeta}>
          {row.customerName} · {formatMalaysianDateTime(row.startsAt)}
        </span>
      </Button>
    </li>
  );
}
