import { Avatar } from "@/components/ui/Avatar";
import styles from "./PersonLabel.module.css";

// A staff member mentioned in running text or a detail: the avatar and the name. Inside another link
// the avatar stays plain, since a link cannot contain a link.
export function PersonLabel({
  id,
  name,
  identity,
  pictureId,
  linked = true,
}: {
  id: string;
  name: string;
  identity: string;
  pictureId: string | null;
  linked?: boolean;
}) {
  return (
    <span className={styles.label}>
      <Avatar
        accountId={id}
        name={name}
        pictureId={pictureId}
        size="sm"
        detail={linked ? identity : undefined}
      />
      <span>{name}</span>
    </span>
  );
}
