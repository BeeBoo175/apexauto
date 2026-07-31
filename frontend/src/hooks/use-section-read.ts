"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { readFailureMessage } from "@/lib/http";

interface SectionReadOptions<T> {
  // Reads the section; throws when the answer is unavailable or belongs to another viewer.
  read: (signal: AbortSignal) => Promise<T>;
  // Identifies the request (query, branch, viewer); a changed key, a refresh or a retry reads again.
  key: string;
  refreshes: number;
  // The toast shown once when a read fails, until a read succeeds again.
  failure: string;
  // A result the page's entry already read: the section opens with it and reads again only when asked to.
  initial?: T | null;
  enabled?: boolean;
}

// One dashboard section: its own bounded read, concurrent with the others, that keeps its last confirmed content
// when a refresh fails and offers a deliberate retry.
export function useSectionRead<T>({
  read,
  key,
  refreshes,
  failure,
  initial = null,
  enabled = true,
}: SectionReadOptions<T>) {
  const [data, setData] = useState<T | null>(initial);
  const [busy, setBusy] = useState(enabled && initial === null);
  const [failed, setFailed] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const toast = useToast();
  const reported = useRef(false);
  const skipFirst = useRef(initial !== null);

  useEffect(() => {
    if (!enabled) {
      return;
    }
    if (skipFirst.current) {
      skipFirst.current = false;
      return;
    }
    const controller = new AbortController();
    let current = true;
    async function load() {
      setBusy(true);
      try {
        const next = await read(controller.signal);
        if (!current) {
          return;
        }
        setData(next);
        setFailed(false);
        reported.current = false;
      } catch (error) {
        if (!current) {
          return;
        }
        setFailed(true);
        if (!reported.current) {
          reported.current = true;
          toast.error(readFailureMessage(error, failure));
        }
      } finally {
        if (current) {
          setBusy(false);
        }
      }
    }
    void load();
    return () => {
      current = false;
      controller.abort();
    };
    // The read, toast and message are stable for a section; the request depends on its key, refreshes and retries.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key, refreshes, attempt, enabled]);

  const retry = useCallback(() => setAttempt((value) => value + 1), []);
  return { data, busy, failed, retry };
}
