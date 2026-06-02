"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import { privateApiFetch } from "@/lib/api-browser";
import { parseStaffTestDriveDetail } from "@/lib/test-drives/staff";
import type { StaffTestDriveDetail } from "@/lib/test-drives/staff-query";

export function useStaffTestDriveDetail(
  actor: CurrentActor,
  initialId: string,
  initial: StaffTestDriveDetail | null,
) {
  const [id, setId] = useState(initialId);
  const [snapshot, setSnapshot] = useState(initial);
  const [pendingId, setPendingId] = useState("");
  const controller = useRef<AbortController | null>(null);
  const { error } = useToast();
  useEffect(() => {
    if (initialId && !initial) {
      error("The selected test drive could not be opened. Retry it from the workspace.");
    }
    return () => controller.current?.abort();
  }, [initialId, initial, error]);

  async function load(nextId = id) {
    if (!nextId) {
      return false;
    }
    controller.current?.abort();
    const attempt = new AbortController();
    controller.current = attempt;
    setPendingId(nextId);
    try {
      const result = await privateApiFetch(
        `/api/staff/test-drives/${nextId}`,
        (value) => {
          const detail = parseStaffTestDriveDetail(value);
          return detail?.booking.id === nextId ? detail : null;
        },
        { signal: attempt.signal },
      );
      if (attempt.signal.aborted) {
        return false;
      }
      if (result.status === 403 || result.status === 404) {
        if (nextId === id) {
          setSnapshot(null);
        }
        throw new Error("Access unavailable");
      }
      if (
        !result.data ||
        result.status !== 200 ||
        actorIdentity(result.data.actor) !== actorIdentity(actor)
      ) {
        throw new Error("Detail unavailable");
      }
      setId(nextId);
      setSnapshot(result.data);
      return true;
    } catch {
      if (!attempt.signal.aborted) {
        error("The test drive could not be opened. Retry to read its current details.");
      }
      return false;
    } finally {
      if (controller.current === attempt) {
        controller.current = null;
        setPendingId("");
      }
    }
  }

  function close() {
    controller.current?.abort();
    controller.current = null;
    setId("");
    setPendingId("");
    setSnapshot(null);
  }

  return { id, snapshot, pendingId, load, close };
}
