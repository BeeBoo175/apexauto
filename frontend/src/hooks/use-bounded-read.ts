"use client";

import { useEffect, useRef, useState } from "react";

// One in-flight read at a time: the owner gets an abortable attempt, and a read that was superseded or
// aborted on unmount never touches state. `ready` flips after hydration so server markup stays stable.
export function useBoundedRead() {
  const [busy, setBusy] = useState(false);
  const [ready, setReady] = useState(false);
  const controller = useRef<AbortController | null>(null);

  useEffect(() => {
    queueMicrotask(() => setReady(true));
    return () => {
      controller.current?.abort();
      controller.current = null;
    };
  }, []);

  // A read for a browser Back or Forward destination supersedes the one in flight instead of being refused.
  function begin(supersede = false) {
    if (controller.current && !supersede) {
      return null;
    }
    controller.current?.abort();
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    return attempt;
  }

  function isCurrent(attempt: AbortController) {
    return !attempt.signal.aborted && controller.current === attempt;
  }

  function end(attempt: AbortController) {
    if (controller.current === attempt) {
      controller.current = null;
      setBusy(false);
    }
  }

  function cancel() {
    controller.current?.abort();
    controller.current = null;
    setBusy(false);
  }

  return { busy, ready, begin, isCurrent, end, cancel };
}
