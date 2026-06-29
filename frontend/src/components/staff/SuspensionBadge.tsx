import { Badge } from "@/components/ui/Badge";
import tones from "@/components/ui/StatusTone.module.css";
import tableStyles from "@/components/staff/StaffTable.module.css";

// A suspended account keeps its work: the badge tells a manager that the handler cannot act or that the customer
// cannot sign in, so the record needs attention without changing its status.
export function SuspensionBadge({ person }: { person: "Handler" | "Customer" }) {
  return (
    <Badge className={`${tones.badge} ${tones.danger} ${tableStyles.status}`}>
      {person} suspended
    </Badge>
  );
}
