"use client";
import { useContext, useEffect } from "react";
import { StaffNavigationLockContext } from "@/components/staff/StaffActorBoundary";

export function useStaffNavigationLock(locked: boolean) {
  const operationLock = useContext(StaffNavigationLockContext);
  useEffect(() => {
    operationLock?.setLocked(locked);
    if (!locked) {
      return;
    }
    const guard = (event: BeforeUnloadEvent) => event.preventDefault();
    window.addEventListener("beforeunload", guard);
    return () => {
      window.removeEventListener("beforeunload", guard);
      operationLock?.setLocked(false);
    };
  }, [locked, operationLock]);
  return (value: boolean) => {
    operationLock?.setLocked(value);
  };
}
