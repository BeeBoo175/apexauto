"use client";

import type { ReactNode } from "react";
import { Button } from "@/components/ui/Button";
import styles from "./StaffQueueWorkspace.module.css";

export function StaffQueueLayout({
  id,
  label,
  selected,
  tabs,
  search,
  queue,
  children,
  className = "",
  inert = false,
  wide = false,
  onBackToQueue,
}: {
  id: string;
  label: string;
  selected: boolean;
  tabs: ReactNode;
  search: ReactNode;
  queue: ReactNode;
  children: ReactNode;
  className?: string;
  inert?: boolean;
  // A wider queue panel, for a tab strip of five or more tabs that needs room to keep counts beside labels.
  wide?: boolean;
  onBackToQueue?: () => void;
}) {
  return (
    <div
      className={`${styles.columns} ${className}`}
      data-selected={selected}
      data-wide={wide}
      inert={inert}
    >
      <aside className={styles.queue} aria-label={label}>
        <div className={styles.queueBody} id={id}>
          {tabs}
          {search}
          {queue}
        </div>
      </aside>
      <div className={styles.workbenchPane}>
        {selected && (
          <div className={styles.mobileBackBar}>
            <Button
              type="button"
              size="compact"
              variant="secondary"
              className={styles.backButton}
              onClick={() => {
                if (onBackToQueue) {
                  onBackToQueue();
                } else if (typeof window !== "undefined") {
                  window.history.back();
                }
              }}
            >
              <span className={styles.backArrow} aria-hidden="true">
                ←
              </span>
              <span>Back to queue</span>
            </Button>
          </div>
        )}
        {children}
      </div>
    </div>
  );
}
