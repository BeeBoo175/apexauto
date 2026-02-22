"use client";
import { useContext, useEffect } from "react";
import { StaffNavigationLockContext } from "@/components/staff/StaffActorBoundary";

export function useInspectionLock(locked: boolean) {
  const navigationLockedRef = useContext(StaffNavigationLockContext);
  useEffect(() => {
    if (navigationLockedRef) {
      navigationLockedRef.current = locked;
    }
    if (!locked) {
      return;
    }
    const guard = (event: BeforeUnloadEvent) => event.preventDefault();
    window.addEventListener("beforeunload", guard);
    return () => {
      window.removeEventListener("beforeunload", guard);
      if (navigationLockedRef) {
        navigationLockedRef.current = false;
      }
    };
  }, [locked, navigationLockedRef]);
  return (value: boolean) => {
    if (navigationLockedRef) {
      navigationLockedRef.current = value;
    }
  };
}
