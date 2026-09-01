"use client";

import type { ReactNode } from "react";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
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
          <nav className={styles.mobileBackBar} aria-label="Queue navigation">
            <Button
              type="button"
              size="compact"
              variant="ghost"
              className={styles.backButton}
              onClick={() => {
                if (onBackToQueue) {
                  onBackToQueue();
                } else if (typeof window !== "undefined") {
                  window.history.back();
                }
              }}
            >
              <Icon name="previous" />
              <span>Back to {label.toLowerCase()}</span>
            </Button>
          </nav>
        )}
        {children}
      </div>
    </div>
  );
}
