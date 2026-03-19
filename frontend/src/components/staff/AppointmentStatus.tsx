import { appointmentStatusLabel, appointmentStatusTone } from "@/lib/staff/status";
import { Badge } from "@/components/ui/Badge";
import tones from "@/components/ui/StatusTone.module.css";
import type { WorklistItem } from "@/lib/staff/contracts";

export function AppointmentStatus({ status }: { status: WorklistItem["status"] }) {
  return (
    <Badge className={`${tones.badge} ${tones[appointmentStatusTone(status)]}`}>
      {appointmentStatusLabel(status)}
    </Badge>
  );
}
