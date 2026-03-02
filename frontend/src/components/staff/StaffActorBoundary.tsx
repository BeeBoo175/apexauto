"use client";

import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode,
  type RefObject,
} from "react";
import { useRouter } from "next/navigation";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import { LoadingProgress, LoadingProgressReady } from "@/components/ui/LoadingProgress";

export const StaffActorContext = createContext<CurrentActor | null>(null);
export const StaffNavigationLockContext = createContext<RefObject<boolean> | null>(null);
export const StaffNavigationContext = createContext<{
  pendingPath: string | null;
  historyPath: string | null;
  completeHistory: () => void;
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
  const router = useRouter();
  const navigation = useContext(StaffNavigationContext);
  const expected = useContext(StaffActorContext);
  const matches = expected && actorIdentity(expected) === actorIdentity(actor);
  const restoring = navigation?.historyPath === path;
  const [cachedSnapshot, setCachedSnapshot] = useState<string | null>(
    restoring ? snapshotId : null,
  );
  if (restoring && !cachedSnapshot) {
    setCachedSnapshot(snapshotId);
  }
  if (!restoring && cachedSnapshot) {
    setCachedSnapshot(null);
  }
  const stale = restoring && (!cachedSnapshot || cachedSnapshot === snapshotId);

  useEffect(() => {
    if (stale) {
      router.refresh();
    }
  }, [stale, router]);

  useEffect(() => {
    if (restoring && cachedSnapshot && cachedSnapshot !== snapshotId) {
      navigation?.completeHistory();
    }
  }, [restoring, cachedSnapshot, snapshotId, navigation]);

  useEffect(() => {
    if (!matches) {
      queueMicrotask(() => {
        window.dispatchEvent(new CustomEvent("apexauto-intake-actor", { detail: actor }));
      });
    }
  }, [actor, matches]);

  if (!matches || stale) {
    return <LoadingProgress label="Checking workspace" />;
  }
  return (
    <>
      <LoadingProgressReady />
      {children}
    </>
  );
}
