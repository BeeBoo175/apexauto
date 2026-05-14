"use client";

import { useCallback, useContext, useLayoutEffect, useMemo, useRef } from "react";
import { DialogEditGuardContext } from "@/components/ui/DialogEditGuardContext";
import type { InteractionBlock } from "@/lib/interaction-guard";
import { useStaffNavigationLock } from "./use-staff-navigation-lock";

interface EditState {
  dirty?: boolean;
  pending?: boolean;
  uncertain?: boolean;
  label: string;
  discard?: () => void;
}

// Leaf editors own their draft reset and captured-operation recovery.
export function useEditGuard(state: boolean | EditState) {
  const dialog = useContext(DialogEditGuardContext);
  const owner = useRef(Symbol("editor"));
  const latestDiscard = useRef<(() => void) | undefined>(undefined);
  const release = useRef<(() => void) | undefined>(undefined);
  const dirty = typeof state !== "boolean" && Boolean(state.dirty);
  const pending = typeof state === "boolean" ? state : Boolean(state.pending);
  const uncertain = typeof state !== "boolean" && Boolean(state.uncertain);
  const label = typeof state === "boolean" ? "Current action" : state.label;
  const canDiscard = typeof state !== "boolean" && Boolean(state.discard);
  useLayoutEffect(() => {
    latestDiscard.current = typeof state === "boolean" ? undefined : state.discard;
  });
  const discard = useCallback(() => {
    latestDiscard.current?.();
    release.current?.();
  }, []);
  const block = useMemo<InteractionBlock | null>(() => {
    if (uncertain) {
      return { kind: "uncertain", label };
    }
    if (pending) {
      return { kind: "pending", label };
    }
    if (dirty) {
      return { kind: "draft", label, discard: canDiscard ? discard : undefined };
    }
    return null;
  }, [uncertain, pending, dirty, label, canDiscard, discard]);
  const navigation = useStaffNavigationLock(block);
  const setLocked = useCallback(
    (value: boolean) => {
      dialog?.setLocked(owner.current, value);
      navigation(value);
    },
    [dialog, navigation],
  );
  useLayoutEffect(() => {
    release.current = () => setLocked(false);
    dialog?.setLocked(owner.current, block);
  });
  useLayoutEffect(() => () => dialog?.setLocked(owner.current, false), [dialog]);
  return setLocked;
}
