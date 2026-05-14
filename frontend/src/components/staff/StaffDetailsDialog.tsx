"use client";

import { useState, type ReactNode } from "react";
import { Dialog } from "@/components/ui/Dialog";
import styles from "./StaffDetailsDialog.module.css";

export type StaffDetail = { label: string; value: ReactNode };

// A quiet link in a section header that opens the record's secondary facts in a modal rather than in the page.
export function StaffDetailsDialog({
  title,
  details,
  triggerLabel = title,
  disabled = false,
}: {
  title: string;
  details: StaffDetail[];
  triggerLabel?: string;
  disabled?: boolean;
}) {
  const [open, setOpen] = useState(false);
  return (
    <div className={styles.details}>
      <button
        type="button"
        className={styles.trigger}
        disabled={disabled}
        onClick={() => setOpen(true)}
      >
        {triggerLabel}
      </button>
      <Dialog open={open} title={title} onClose={() => setOpen(false)}>
        <dl className={styles.facts}>
          {details.map((detail) => (
            <div key={detail.label}>
              <dt>{detail.label}</dt>
              <dd>{detail.value}</dd>
            </div>
          ))}
        </dl>
      </Dialog>
    </div>
  );
}
