import type { ReactNode } from "react";
import { Badge } from "@/components/ui/Badge";
import tones from "@/components/ui/StatusTone.module.css";
import tableStyles from "@/components/staff/StaffTable.module.css";
import styles from "./SuspensionBadge.module.css";

type Person = "Handler" | "Customer";

// A suspended account keeps its work: the badge tells a manager that the handler cannot act or that the customer
// cannot sign in, so the record needs attention without changing its status.
export function SuspensionBadge({ person }: { person: Person }) {
  return (
    <Badge className={`${tones.badge} ${tones.danger} ${tableStyles.status}`}>
      {person} suspended
    </Badge>
  );
}

// A table cell narrower than the name and the badge together would clip the badge, so it sits on its own line.
export function SuspendedPerson({
  person,
  suspended,
  children,
}: {
  person: Person;
  suspended: boolean;
  children: ReactNode;
}) {
  if (!suspended) {
    return children;
  }
  return (
    <span className={styles.stack}>
      {children}
      <SuspensionBadge person={person} />
    </span>
  );
}
