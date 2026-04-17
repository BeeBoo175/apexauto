"use client";

import React, { useEffect, useId, useRef } from "react";
import { CloseButton } from "./CloseButton";
import styles from "./Dialog.module.css";

export interface DialogProps {
  open: boolean;
  onClose: () => void;
  title: string;
  description?: string;
  headerActions?: React.ReactNode;
  children: React.ReactNode;
  closeDisabled?: boolean;
  variant?: "dialog" | "drawer";
  density?: "default" | "compact";
  width?: "default" | "wide";
  layout?: "flow" | "workspace";
}

export function DialogActionSlot({ ref }: { ref: React.Ref<HTMLDivElement> }) {
  return <div ref={ref} className={styles.actionSlot} />;
}

export const Dialog: React.FC<DialogProps> = ({
  open,
  onClose,
  title,
  description,
  headerActions,
  children,
  closeDisabled = false,
  variant = "dialog",
  density = "default",
  width = "default",
  layout = "flow",
}) => {
  const element = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  const descriptionId = useId();
  useEffect(() => {
    if (!open || !element.current) {
      return;
    }
    const previous = document.activeElement;
    const dialog = element.current;
    const overflow = document.body.style.overflow;
    const stopPointer = (event: MouseEvent) => event.stopPropagation();
    const stopEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.stopPropagation();
      }
    };
    // Native listeners run before document-level background disclosure dismissal.
    dialog.addEventListener("mousedown", stopPointer);
    dialog.addEventListener("keydown", stopEscape);
    dialog.showModal();
    document.dispatchEvent(new Event("apexauto-dialog-change"));
    document.body.style.overflow = "hidden";
    return () => {
      dialog.close();
      document.dispatchEvent(new Event("apexauto-dialog-change"));
      dialog.removeEventListener("mousedown", stopPointer);
      dialog.removeEventListener("keydown", stopEscape);
      document.body.style.overflow = overflow;
      if (
        previous instanceof HTMLElement &&
        previous.isConnected &&
        previous !== document.body &&
        previous !== document.documentElement
      ) {
        previous.focus();
      } else {
        document.querySelector<HTMLElement>("main h1")?.focus();
      }
    };
  }, [open]);

  if (!open) {
    return null;
  }

  return (
    <dialog
      ref={element}
      aria-labelledby={titleId}
      aria-describedby={description ? descriptionId : undefined}
      onCancel={(event) => {
        event.preventDefault();
        if (!closeDisabled) {
          onClose();
        }
      }}
      onKeyDown={(event) => {
        if (event.key !== "Tab") {
          return;
        }
        const controls = Array.from(
          event.currentTarget.querySelectorAll<HTMLElement>(
            "button:not(:disabled), a[href], input:not(:disabled), select:not(:disabled), textarea:not(:disabled)",
          ),
        ).filter((control) => control.getClientRects().length > 0);
        const first = controls[0];
        const last = controls.at(-1);
        if (!event.shiftKey && document.activeElement === last) {
          event.preventDefault();
          first?.focus();
        }
        if (event.shiftKey && document.activeElement === first) {
          event.preventDefault();
          last?.focus();
        }
      }}
      className={`${styles.surface} ${styles[variant]} ${density === "compact" ? styles.compact : ""} ${width === "wide" ? styles.wide : ""} ${layout === "workspace" ? styles.workspace : ""}`}
    >
      <div className={styles.header}>
        <div>
          <h2 id={titleId} className={styles.title}>
            {title}
          </h2>
          {description && (
            <p id={descriptionId} className={styles.description}>
              {description}
            </p>
          )}
        </div>
        <div className={styles.actions}>
          {headerActions}
          <CloseButton
            size={variant === "drawer" || density === "compact" ? "compact" : "md"}
            onClick={onClose}
            disabled={closeDisabled}
          />
        </div>
      </div>
      <div className={styles.content}>{children}</div>
    </dialog>
  );
};
