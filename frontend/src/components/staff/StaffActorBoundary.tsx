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
export const StaffHistoryContext = createContext<{
  path: string | null;
  complete: () => void;
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
  const history = useContext(StaffHistoryContext);
  const expected = useContext(StaffActorContext);
  const matches = expected && actorIdentity(expected) === actorIdentity(actor);
  const restoring = history?.path === path;
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
      history?.complete();
    }
  }, [restoring, cachedSnapshot, snapshotId, history]);

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
