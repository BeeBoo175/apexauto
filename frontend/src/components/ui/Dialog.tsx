"use client";

import React, { useEffect, useId, useRef } from "react";
import { CloseButton } from "./CloseButton";

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
}) => {
  const element = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  const descriptionId = useId();
  const drawerWidth = width === "wide" ? "max-w-[1000px]" : "max-w-[520px]";
  const placement =
    variant === "drawer"
      ? `inset-y-0 left-auto right-0 m-0 h-dvh max-h-dvh w-full ${drawerWidth}`
      : "inset-0 m-auto w-[calc(100%-32px)] max-w-lg max-h-[90dvh] rounded-sm";
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
      className={`fixed bg-paper text-graphite border border-line backdrop:bg-graphite/60 overflow-y-auto ${density === "compact" ? "p-5" : "p-6"} ${placement}`}
    >
      <div className="flex items-center justify-between gap-4 border-b border-line-soft pb-4 mb-4">
        <div>
          <h2
            id={titleId}
            className={`${density === "compact" ? "text-base" : "text-xl"} font-bold text-graphite`}
          >
            {title}
          </h2>
          {description && (
            <p id={descriptionId} className="text-sm text-muted mt-2">
              {description}
            </p>
          )}
        </div>
        <div className="flex shrink-0 items-center gap-2">
          {headerActions}
          <CloseButton
            size={density === "compact" ? "compact" : "md"}
            onClick={onClose}
            disabled={closeDisabled}
          />
        </div>
      </div>
      <div>{children}</div>
    </dialog>
  );
};
