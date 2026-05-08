import { Badge } from "@/components/ui/Badge";
import { saleStatusLabels, type SaleStatus as Status } from "@/lib/sales/contracts";
import tones from "@/components/ui/StatusTone.module.css";
import tableStyles from "@/components/staff/StaffTable.module.css";

const statusTones: Record<Status, string> = {
  Open: tones.info,
  Handling: tones.info,
  PendingApproval: tones.warning,
  Approved: tones.success,
  Expired: tones.neutral,
  Cancelled: tones.neutral,
};

// The sale's status as one badge, shared by the queue, the table and the sale workspace.
export function SaleStatus({ status, table = false }: { status: Status; table?: boolean }) {
  return (
    <Badge className={`${tones.badge} ${statusTones[status]} ${table ? tableStyles.status : ""}`}>
      {saleStatusLabels[status]}
    </Badge>
  );
}
