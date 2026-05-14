"use client";

import React, {
  useEffect,
  useEffectEvent,
  useId,
  useLayoutEffect,
  useRef,
  useState,
  useSyncExternalStore,
} from "react";
import { createInteractionGuard } from "@/lib/interaction-guard";
import { DialogEditGuardContext } from "./DialogEditGuardContext";
import { CloseButton } from "./CloseButton";
import { EditGuardFeedback } from "./EditGuardFeedback";
import styles from "./Dialog.module.css";

export interface DialogProps {
  open: boolean;
  onClose: () => void;
  title: string;
  description?: string;
  headerActions?: React.ReactNode;
  headerLoading?: boolean;
  descriptionPlaceholder?: boolean;
  children: React.ReactNode;
  closeDisabled?: boolean;
  showCloseButton?: boolean;
  appearance?: "default" | "confirmation";
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
  headerLoading = false,
  descriptionPlaceholder = true,
  children,
  closeDisabled = false,
  showCloseButton = true,
  appearance = "default",
  variant = "dialog",
  density = "default",
  width = "default",
  layout = "flow",
}) => {
  const element = useRef<HTMLDialogElement>(null);
  const [guard] = useState(createInteractionGuard);
  const guardState = useSyncExternalStore(guard.subscribe, guard.getSnapshot, guard.getSnapshot);
  const explicitOwner = useRef(Symbol("dialog operation"));
  useLayoutEffect(() => {
    const owner = explicitOwner.current;
    guard.setLocked(owner, closeDisabled);
    return () => guard.setLocked(owner, false);
  }, [guard, closeDisabled]);
  const dismissalLocked = closeDisabled || guardState.blocked;
  const dismiss = () => {
    guard.request(onClose, "Discard and close");
  };
  const cancel = useEffectEvent((event: Event) => {
    if (event.target !== element.current) {
      return;
    }
    event.preventDefault();
    event.stopPropagation();
    dismiss();
  });
  const escape = useEffectEvent((event: KeyboardEvent) => {
    if (
      event.key !== "Escape" ||
      !(event.target instanceof Element) ||
      event.target.closest("dialog") !== element.current
    ) {
      return;
    }
    event.preventDefault();
    dismiss();
  });
  const closed = useEffectEvent(() => {
    // Strict Mode may reopen the dialog before a cleanup's queued close event arrives.
    if (element.current?.open) {
      return;
    }
    if (closeDisabled || guard.isLocked()) {
      element.current?.showModal();
      dismiss();
      return;
    }
    onClose();
  });
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
    // Native listeners run before document-level background disclosure dismissal.
    dialog.addEventListener("mousedown", stopPointer);
    dialog.addEventListener("keydown", escape);
    dialog.addEventListener("cancel", cancel);
    dialog.addEventListener("close", closed);
    dialog.showModal();
    document.dispatchEvent(new Event("apexauto-dialog-change"));
    document.body.style.overflow = "hidden";
    return () => {
      dialog.removeEventListener("mousedown", stopPointer);
      dialog.removeEventListener("keydown", escape);
      dialog.removeEventListener("cancel", cancel);
      dialog.removeEventListener("close", closed);
      dialog.close();
      document.dispatchEvent(new Event("apexauto-dialog-change"));
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
      aria-describedby={!headerLoading && description ? descriptionId : undefined}
      onKeyDown={(event) => {
        if (event.key === "Escape") {
          event.stopPropagation();
          return;
        }
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
      className={`${styles.surface} ${styles[variant]} ${density === "compact" ? styles.compact : ""} ${width === "wide" ? styles.wide : ""} ${layout === "workspace" ? styles.workspace : ""} ${appearance === "confirmation" ? styles.confirmation : ""}`}
    >
      <div className={styles.header}>
        <div>
          <h2 id={titleId} className={styles.title}>
            {title}
          </h2>
          {headerLoading && descriptionPlaceholder && (
            <span className={styles.descriptionSkeleton} aria-hidden="true" />
          )}
          {!headerLoading && description && (
            <p id={descriptionId} className={styles.description}>
              {description}
            </p>
          )}
        </div>
        {(headerActions || headerLoading || showCloseButton) && (
          <div className={styles.actions}>
            {headerLoading ? (
              <span className={styles.actionSkeleton} aria-hidden="true" />
            ) : (
              headerActions
            )}
            {showCloseButton && (
              <CloseButton
                label={variant === "drawer" ? "Close drawer" : "Close dialog"}
                onClick={dismiss}
                disabled={dismissalLocked}
                title={guardState.message || undefined}
              />
            )}
          </div>
        )}
      </div>
      <DialogEditGuardContext.Provider value={guard}>
        <div className={styles.content}>{children}</div>
        <EditGuardFeedback guard={guard} surface={Dialog} />
      </DialogEditGuardContext.Provider>
    </dialog>
  );
};
