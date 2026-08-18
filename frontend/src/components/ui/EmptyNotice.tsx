import { Button } from "./Button";
import { Icon } from "./Icon";
import type { IconName } from "@/lib/icons";
import styles from "./EmptyNotice.module.css";

// The guidance text with the first mention of a place in the workspace turned into a link that goes there.
export function EmptyNoticeGuidance({
  text,
  link,
}: {
  text: string;
  link?: { text: string; onClick: () => void };
}) {
  const start = link ? text.indexOf(link.text) : -1;
  if (!link || start < 0) {
    return text;
  }

  return (
    <>
      {text.slice(0, start)}
      <button type="button" className={styles.link} onClick={link.onClick}>
        {link.text}
      </button>
      {text.slice(start + link.text.length)}
    </>
  );
}

// The shared message for a list or table that has no rows, has no match for the search or could not be read, so
// every such place shows the same centred icon, title, one line of guidance and recovery action instead of its own
// text. An alert tone colours the icon red for a read that failed; every other state is blue. Keep the guidance short
// enough for one line in the narrowest panel.
export function EmptyNotice({
  icon,
  tone = "info",
  title,
  children,
  link,
  action,
}: {
  icon: IconName;
  tone?: "info" | "alert";
  title: string;
  children: string;
  // A place named in the guidance, such as another tab, that the guidance links to.
  link?: { text: string; onClick: () => void };
  action?: { label: string; onClick: () => void; disabled?: boolean };
}) {
  return (
    <div className={styles.notice}>
      <span className={styles.icon} data-tone={tone}>
        <Icon name={icon} />
      </span>
      <p className={styles.title}>{title}</p>
      <p className={styles.text}>
        <EmptyNoticeGuidance text={children} link={link} />
      </p>
      {action && (
        <Button
          size="compact"
          variant="primary"
          className={styles.action}
          disabled={action.disabled}
          onClick={action.onClick}
        >
          {action.label}
        </Button>
      )}
    </div>
  );
}
