"use client";
import { useCallback, useContext, useLayoutEffect, useRef } from "react";
import { StaffNavigationLockContext } from "@/components/staff/StaffNavigationLockContext";
import type { InteractionBlock } from "@/lib/interaction-guard";

export function useStaffNavigationLock(locked: boolean | InteractionBlock | null) {
  const operationLock = useContext(StaffNavigationLockContext);
  const owner = useRef(Symbol("navigation editor"));
  const setLocked = useCallback(
    (value: boolean | InteractionBlock | null) => {
      operationLock?.setLocked(value, owner.current);
    },
    [operationLock],
  );
  useLayoutEffect(() => {
    setLocked(locked);
  });
  useLayoutEffect(() => () => setLocked(false), [setLocked]);
  useLayoutEffect(() => {
    if (!locked) {
      return;
    }
    const guard = (event: BeforeUnloadEvent) => event.preventDefault();
    window.addEventListener("beforeunload", guard);
    return () => {
      window.removeEventListener("beforeunload", guard);
    };
  }, [locked]);
  return setLocked;
}
