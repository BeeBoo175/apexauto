import { Badge } from "@/components/ui/Badge";
import { testDriveStatusLabels } from "@/lib/test-drives/account";
import type { StaffTestDriveItem } from "@/lib/test-drives/staff-query";
import tones from "@/components/ui/StatusTone.module.css";
import tableStyles from "@/components/staff/StaffTable.module.css";

const statusTones = {
  Booked: "info",
  InProgress: "info",
  Completed: "success",
  NotCompleted: "warning",
  NoShow: "warning",
  Cancelled: "neutral",
} as const;

export function StaffTestDriveStatus({
  status,
  table = false,
}: {
  status: StaffTestDriveItem["status"];
  table?: boolean;
}) {
  return (
    <Badge
      className={`${tones.badge} ${tones[statusTones[status]]} ${table ? tableStyles.status : ""}`}
    >
      {testDriveStatusLabels[status]}
    </Badge>
  );
}
