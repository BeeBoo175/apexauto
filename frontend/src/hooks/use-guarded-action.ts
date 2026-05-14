"use client";

import { useCallback, useContext, useSyncExternalStore } from "react";
import { StaffNavigationLockContext } from "@/components/staff/StaffNavigationLockContext";

const subscribe = () => () => {};
const emptyState = { blocked: false, message: "" };
const unlocked = () => emptyState;

export function useGuardedAction() {
  const context = useContext(StaffNavigationLockContext);
  const guard = context?.guard;
  const state = useSyncExternalStore<{ blocked: boolean; message: string }>(
    guard?.subscribe ?? subscribe,
    guard?.getSnapshot ?? unlocked,
    unlocked,
  );
  const request = useCallback(
    (action: () => void, label?: string) => {
      if (guard) {
        guard.request(action, label);
        return;
      }
      action();
    },
    [guard],
  );
  return { request, blocked: state.blocked, message: state.message || undefined };
}
