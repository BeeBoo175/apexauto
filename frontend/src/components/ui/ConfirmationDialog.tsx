"use client";

import type { ReactNode } from "react";
import { ConfirmationActions } from "./ConfirmationActions";
import { Dialog } from "./Dialog";
import styles from "./ConfirmationDialog.module.css";

export function ConfirmationDialog({
  open,
  title,
  children,
  confirmLabel,
  cancelLabel = "Keep editing",
  danger = false,
  busy = false,
  locked = false,
  facts = [],
  onClose,
  onConfirm,
}: {
  open: boolean;
  title: string;
  children: ReactNode;
  confirmLabel: string;
  cancelLabel?: string;
  danger?: boolean;
  busy?: boolean;
  locked?: boolean;
  facts?: readonly { label: string; value: ReactNode }[];
  onClose: () => void;
  onConfirm: () => void | Promise<void>;
}) {
  return (
    <Dialog
      open={open}
      title={title}
      density="compact"
      appearance="confirmation"
      showCloseButton={false}
      closeDisabled={busy || locked}
      onClose={onClose}
    >
      {facts.length > 0 && (
        <dl className={styles.facts}>
          {facts.map((fact) => (
            <div key={fact.label}>
              <dt>{fact.label}</dt>
              <dd>{fact.value}</dd>
            </div>
          ))}
        </dl>
      )}
      <div className={styles.message}>{children}</div>
      <ConfirmationActions
        confirmLabel={confirmLabel}
        cancelLabel={cancelLabel}
        busy={busy}
        locked={locked}
        danger={danger}
        onClose={onClose}
        onConfirm={onConfirm}
      />
    </Dialog>
  );
}
