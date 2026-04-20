import { appointmentStatusLabel, appointmentStatusTone } from "@/lib/staff/status";
import { Badge } from "@/components/ui/Badge";
import styles from "./StaffTable.module.css";
import tones from "@/components/ui/StatusTone.module.css";
import type { WorklistItem } from "@/lib/staff/contracts";

export function AppointmentStatus({
  status,
  table = false,
}: {
  status: WorklistItem["status"];
  table?: boolean;
}) {
  return (
    <Badge
      className={`${tones.badge} ${tones[appointmentStatusTone(status)]} ${table ? styles.status : ""}`}
    >
      {appointmentStatusLabel(status)}
    </Badge>
  );
}
