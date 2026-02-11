import { appointmentStatusLabel } from "@/lib/staff/status";
import { Badge } from "@/components/ui/Badge";
import tones from "@/components/ui/StatusTone.module.css";
import type { WorklistItem } from "@/lib/staff/contracts";

const statusTones = {
  Booked: "info",
  CheckedIn: "success",
  Cancelled: "neutral",
  NoShow: "neutral",
} as const;

export function AppointmentStatus({ status }: { status: WorklistItem["status"] }) {
  return (
    <Badge className={`${tones.badge} ${tones[statusTones[status]]}`}>
      {appointmentStatusLabel(status)}
    </Badge>
  );
}
