"use client";

import { createContext, useContext, useEffect, type ReactNode, type RefObject } from "react";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import { LoadingProgress, LoadingProgressReady } from "@/components/ui/LoadingProgress";
import { StaffEntryIdentityContext } from "./StaffClientEntryBoundary";

export const StaffActorContext = createContext<CurrentActor | null>(null);
export const StaffNavigationLockContext = createContext<{
  lockedRef: RefObject<boolean>;
  setLocked: (locked: boolean) => void;
} | null>(null);
export const StaffPageEntranceContext = createContext(false);
export const StaffNavigationContext = createContext<{
  pendingPath: string | null;
  entryPath: string | null;
  recordNavigation: (href: string) => void;
} | null>(null);

export function StaffActorBoundary({
  actor,
  children,
  path,
  snapshotId,
}: {
  actor: CurrentActor;
  children: ReactNode;
  path: string;
  snapshotId: string;
}) {
  const navigation = useContext(StaffNavigationContext);
  const expected = useContext(StaffActorContext);
  const matches = expected && actorIdentity(expected) === actorIdentity(actor);
  useEffect(() => {
    if (!matches) {
      queueMicrotask(() => {
        window.dispatchEvent(new CustomEvent("apexauto-intake-actor", { detail: actor }));
      });
    }
  }, [actor, matches]);

  if (!matches) {
    return <LoadingProgress label="Checking workspace" />;
  }
  return (
    <StaffPageEntranceContext.Provider value={navigation?.entryPath === path}>
      <LoadingProgressReady />
      <StaffEntryIdentityContext.Provider value={snapshotId}>
        {children}
      </StaffEntryIdentityContext.Provider>
    </StaffPageEntranceContext.Provider>
  );
}
