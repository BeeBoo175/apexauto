"use client";

import { useState, type ReactNode } from "react";
import { Button, type ButtonProps } from "./Button";
import { ConfirmationDialog } from "./ConfirmationDialog";

export function ConfirmationButton({
  confirmationTitle,
  message,
  onConfirm,
  skipConfirmation = false,
  children,
  ...props
}: Omit<ButtonProps, "onClick" | "type"> & {
  confirmationTitle: string;
  message: ReactNode;
  onConfirm: () => void | Promise<unknown>;
  skipConfirmation?: boolean;
}) {
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);

  async function confirm() {
    if (busy) {
      return;
    }
    setBusy(true);
    try {
      await onConfirm();
    } finally {
      setBusy(false);
      setOpen(false);
    }
  }

  return (
    <>
      <Button
        {...props}
        type="button"
        disabled={props.disabled || busy}
        onClick={() => {
          if (skipConfirmation) {
            void confirm();
            return;
          }
          setOpen(true);
        }}
      >
        {children}
      </Button>
      <ConfirmationDialog
        open={open}
        title={confirmationTitle}
        confirmLabel={typeof children === "string" ? children : "Confirm action"}
        danger={props.variant === "danger"}
        busy={busy}
        onClose={() => setOpen(false)}
        onConfirm={confirm}
      >
        {message}
      </ConfirmationDialog>
    </>
  );
}
