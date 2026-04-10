"use client";

import { startTransition, use, useContext, useEffect, useState, useSyncExternalStore } from "react";
import {
  StaffEntryIdentityContext,
  StaffEntryRequestsContext,
} from "@/components/staff/StaffClientEntryBoundary";
import { useToast } from "@/components/ui/Toast";

const subscribe = () => () => {};
const clientSnapshot = () => true;
const serverSnapshot = () => false;

export function useStaffClientEntry<T>(
  read: (signal: AbortSignal) => Promise<T>,
  failureMessage: string,
) {
  const requests = useContext(StaffEntryRequestsContext);
  const id = useContext(StaffEntryIdentityContext);
  const { error } = useToast();
  const hydrated = useSyncExternalStore(subscribe, clientSnapshot, serverSnapshot);
  if (!requests || !id) {
    throw new Error("Staff entry requires its authenticated frame");
  }
  const [request, setRequest] = useState(() => (hydrated ? requests.open(id, read) : null));
  const entry = request ? use(request.promise) : null;

  useEffect(() => {
    if (!request) {
      startTransition(() => setRequest(requests.open(id, read)));
    }
  }, [request, requests, id, read]);

  useEffect(() => {
    if (!request) {
      return;
    }
    requests.release(id);
    if (!entry) {
      error(failureMessage);
    }
    return () => request.controller.abort();
  }, [requests, id, request, entry, error, failureMessage]);

  const retry = () => {
    setRequest(requests.open(id, read));
  };

  return { entry, failed: Boolean(request) && !entry, retry };
}
