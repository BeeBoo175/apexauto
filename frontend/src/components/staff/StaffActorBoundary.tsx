"use client";

import { createContext, useContext, useEffect, type ReactNode } from "react";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import {
  StaffEntryIdentityContext,
  StaffEntryPresentationContext,
} from "./StaffClientEntryBoundary";

export const StaffActorContext = createContext<CurrentActor | null>(null);
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
  parameters,
  snapshotId,
}: {
  actor: CurrentActor;
  children: ReactNode;
  path: string;
  parameters: SearchParams;
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
      <StaffEntryIdentityContext.Provider value={snapshotId}>
        <StaffEntryPresentationContext.Provider value={{ actor, path, parameters }}>
          {children}
        </StaffEntryPresentationContext.Provider>
      </StaffEntryIdentityContext.Provider>
    </StaffPageEntranceContext.Provider>
  );
}
