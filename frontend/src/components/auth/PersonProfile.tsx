import { Avatar } from "@/components/ui/Avatar";
import type { Person } from "@/lib/account/contracts";
import { roleNames } from "@/lib/auth/actor";
import styles from "./AccountPanel.module.css";

// The read-only counterpart of the account panel; details the viewer may not see arrive as null.
export function PersonProfile({ person }: { person: Person }) {
  const details = [
    { label: "Username", value: person.username },
    { label: "Role", value: roleNames[person.role] },
    { label: "Branch", value: person.branchName },
    { label: "Email", value: person.email },
    { label: "Work mobile", value: person.mobile },
  ].filter((detail) => detail.value);

  return (
    <div className={styles.panel}>
      <div className={styles.profile}>
        <Avatar
          accountId={person.userId}
          name={person.name}
          pictureId={person.pictureId}
          size="lg"
        />
        <div className={styles.identity}>
          <p className={styles.name}>{person.name}</p>
          <p className={styles.email}>@{person.username}</p>
        </div>
        {!person.isActive && <span className={styles.verificationState}>Deactivated</span>}
      </div>
      <dl className={styles.rows}>
        {details.map((detail) => (
          <div key={detail.label} className={styles.row}>
            <dt className={styles.label}>{detail.label}</dt>
            <dd className={styles.value}>{detail.value}</dd>
          </div>
        ))}
      </dl>
    </div>
  );
}
