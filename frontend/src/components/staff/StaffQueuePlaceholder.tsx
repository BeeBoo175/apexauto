import { StaffQueueGuidance } from "./StaffQueueEmpty";
import styles from "./StaffQueueWorkspace.module.css";

// The shared prompt for a queue workspace's content pane when nothing is selected, so every queue page
// shows the same centred title and one line of guidance instead of its own empty message.
export function StaffQueuePlaceholder({
  label,
  title,
  link,
  children,
}: {
  label: string;
  title: string;
  // A place named in the guidance, such as another tab, that the guidance links to.
  link?: { text: string; onClick: () => void };
  children: string;
}) {
  return (
    <section className={styles.placeholder} aria-label={label}>
      <h2>{title}</h2>
      <p>
        <StaffQueueGuidance text={children} link={link} />
      </p>
    </section>
  );
}
