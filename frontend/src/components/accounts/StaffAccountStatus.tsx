import { Badge } from "@/components/ui/Badge";
import { staffAccountStatusLabels } from "@/lib/accounts/staff-accounts";
import tones from "@/components/ui/StatusTone.module.css";
import tableStyles from "@/components/staff/StaffTable.module.css";

const statusTones = {
  Active: "success",
  Deactivated: "neutral",
  Invited: "info",
  Suspended: "danger",
  InvitationExpired: "warning",
} as const;

export function StaffAccountStatus({ status }: { status: string }) {
  const tone = statusTones[status as keyof typeof statusTones] ?? "neutral";
  return (
    <Badge className={`${tones.badge} ${tones[tone]} ${tableStyles.status}`}>
      {staffAccountStatusLabels[status] ?? status}
    </Badge>
  );
}
