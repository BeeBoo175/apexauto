import type { ComponentPropsWithoutRef, ReactNode, Ref } from "react";
import { formatMalaysianDateTime } from "@/lib/date-format";
import styles from "./StaffHistory.module.css";

// The vertical event rail every staff history uses: newest first, one dated heading per event.
export function StaffTimeline({
  listRef,
  children,
}: {
  listRef?: Ref<HTMLOListElement>;
  children: ReactNode;
}) {
  return (
    <ol ref={listRef} className={styles.timeline}>
      {children}
    </ol>
  );
}

export function StaffTimelineEvent({
  title,
  at,
  context,
  children,
  ...props
}: Omit<ComponentPropsWithoutRef<"li">, "title"> & {
  title: string;
  at: string;
  context?: string;
}) {
  return (
    <li {...props}>
      <div className={styles.eventHeading}>
        <h3>{title}</h3>
        <time dateTime={at}>{formatMalaysianDateTime(at)}</time>
      </div>
      {context && <span className={styles.context}>{context}</span>}
      {children}
    </li>
  );
}
