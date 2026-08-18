"use client";

import type { ReactNode } from "react";
import { Button } from "@/components/ui/Button";
import styles from "./StaffQueueWorkspace.module.css";

export function StaffQueueLayout({
  id,
  label,
  title,
  selected,
  expanded,
  onExpandedChange,
  tabs,
  search,
  queue,
  children,
  className = "",
  inert = false,
  wide = false,
}: {
  id: string;
  label: string;
  title: string;
  selected: boolean;
  expanded: boolean;
  onExpandedChange: (expanded: boolean) => void;
  tabs: ReactNode;
  search: ReactNode;
  queue: ReactNode;
  children: ReactNode;
  className?: string;
  inert?: boolean;
  // A wider queue panel, for a tab strip of five or more tabs that needs room to keep counts beside labels.
  wide?: boolean;
}) {
  return (
    <div
      className={`${styles.columns} ${className}`}
      data-selected={selected}
      data-wide={wide}
      inert={inert}
    >
      <aside className={styles.queue} aria-label={label} data-expanded={expanded}>
        {selected && (
          <div className={styles.mobileQueueBar}>
            <strong>{title}</strong>
            <Button
              size="compact"
              variant="secondary"
              aria-expanded={expanded}
              aria-controls={id}
              onClick={() => onExpandedChange(!expanded)}
            >
              {expanded ? "Hide queue" : "Show queue"}
            </Button>
          </div>
        )}
        <div className={styles.queueBody} id={id}>
          {tabs}
          {search}
          {queue}
        </div>
      </aside>
      {children}
    </div>
  );
}
