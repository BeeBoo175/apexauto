"use client";

import { Button } from "./Button";
import styles from "./ConfirmationDialog.module.css";

export function ConfirmationActions({
  confirmLabel,
  cancelLabel,
  busy = false,
  locked = false,
  danger = false,
  onClose,
  onConfirm,
}: {
  confirmLabel: string;
  cancelLabel: string;
  busy?: boolean;
  locked?: boolean;
  danger?: boolean;
  onClose: () => void;
  onConfirm: () => void | Promise<void>;
}) {
  return (
    <div className={styles.actions}>
      <Button
        type="button"
        size="compact"
        variant="ghost"
        autoFocus
        disabled={busy || locked}
        onClick={onClose}
      >
        {cancelLabel}
      </Button>
      <Button
        type="button"
        size="compact"
        variant={danger ? "danger" : "primary"}
        disabled={busy}
        aria-busy={busy}
        onClick={() => void onConfirm()}
      >
        {busy ? "Working…" : confirmLabel}
      </Button>
    </div>
  );
}
