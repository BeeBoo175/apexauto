"use client";

import { useEffect, useSyncExternalStore, type ComponentType } from "react";
import type { InteractionGuard } from "@/lib/interaction-guard";
import type { DialogProps } from "./Dialog";
import { ConfirmationActions } from "./ConfirmationActions";
import { useToast } from "./Toast";
import styles from "./ConfirmationDialog.module.css";

export function EditGuardFeedback({
  guard,
  surface: Surface,
}: {
  guard: InteractionGuard;
  surface: ComponentType<DialogProps>;
}) {
  const state = useSyncExternalStore(guard.subscribe, guard.getSnapshot, guard.getSnapshot);
  const { warning } = useToast();
  useEffect(() => {
    if (state.notice) {
      warning(state.notice.message);
      guard.clearNotice();
    }
  }, [state.notice, warning, guard]);

  if (!state.intent) {
    return null;
  }
  return (
    <Surface
      open
      title="Discard unsaved changes?"
      density="compact"
      appearance="confirmation"
      showCloseButton={false}
      onClose={guard.cancel}
    >
      <div className={styles.message}>
        <p>Your unsaved entries in this editor will be discarded. Saved records stay unchanged.</p>
      </div>
      <ConfirmationActions
        confirmLabel={state.intent.label}
        cancelLabel="Keep editing"
        danger
        onClose={guard.cancel}
        onConfirm={guard.confirm}
      />
    </Surface>
  );
}
