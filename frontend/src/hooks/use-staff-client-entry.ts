"use client";

import { use, useContext, useEffect, useState, useSyncExternalStore, useTransition } from "react";
import {
  StaffEntryIdentityContext,
  StaffEntryRequestsContext,
} from "@/components/staff/StaffClientEntryBoundary";
import { useLoadingProgress, useLoadingProgressReady } from "@/components/ui/LoadingProgress";

const subscribe = () => () => {};
const clientSnapshot = () => true;
const serverSnapshot = () => false;

export function useStaffClientEntry<T>(read: (signal: AbortSignal) => Promise<T>) {
  const requests = useContext(StaffEntryRequestsContext);
  const id = useContext(StaffEntryIdentityContext);
  const [pending, startTransition] = useTransition();
  const hydrated = useSyncExternalStore(subscribe, clientSnapshot, serverSnapshot);
  if (!requests || !id) {
    throw new Error("Staff entry requires its authenticated frame");
  }
  // A new frame must commit before its request owner can survive suspension.
  const [request, setRequest] = useState(() =>
    hydrated && requests.isMounted() ? requests.open(id, read) : null,
  );
  const entry = request ? use(request.promise) : null;
  useLoadingProgress("Opening workspace", pending);
  useLoadingProgressReady(request !== null);

  useEffect(() => {
    if (!request && !pending) {
      startTransition(() => setRequest(requests.open(id, read)));
    }
  }, [request, requests, id, read, pending]);

  useEffect(() => {
    if (!request) {
      return;
    }
    requests.release(id);
    return () => request.controller.abort();
  }, [requests, id, request]);

  return { entry, failed: Boolean(request) && !entry };
}
