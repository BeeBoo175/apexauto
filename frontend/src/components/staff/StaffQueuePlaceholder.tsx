import styles from "./StaffQueueWorkspace.module.css";

// The shared prompt for a queue workspace's content pane when nothing is selected, so every queue page
// shows the same centred title and guidance instead of its own empty message.
export function StaffQueuePlaceholder({
  label,
  title,
  children,
}: {
  label: string;
  title: string;
  children: string;
}) {
  return (
    <section className={styles.placeholder} aria-label={label}>
      <h2>{title}</h2>
      <p>{children}</p>
    </section>
  );
}
