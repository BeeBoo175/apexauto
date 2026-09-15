"use client";

import { useEffect, useRef, useState } from "react";
import { isUnconfirmedResult } from "@/hooks/use-api-command";
import { privateApiFetch } from "@/lib/api-browser";
import {
  CUSTOMER_CLOSURE_BLOCKERS_PATH,
  CUSTOMER_CLOSURE_PATH,
  closureConflict,
  parseClosurePreview,
  parseClosureReceipt,
  type ClosurePreview,
  type ClosureReceipt,
} from "@/lib/accounts/customer-closure";

type PreviewState =
  { status: "loading" } | { status: "failed" } | { status: "ready"; preview: ClosurePreview };

// Reads what blocks closure and what stays, then sends the one irreversible confirmation. The preview is read each
// time the dialog opens because a blocker may have been resolved or created since. A confirmation that the browser
// could not confirm keeps its exact body and operation id: replaying it either returns the stored receipt or, when
// the closure had already happened, meets a closed session (401), which is reported as closed rather than as an error.
export function useAccountClosure(enabled: boolean) {
  const [state, setState] = useState<PreviewState>({ status: "loading" });
  const [reads, setReads] = useState(0);
  const [busy, setBusy] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  const [problem, setProblem] = useState("");
  const [receipt, setReceipt] = useState<ClosureReceipt | null>(null);
  const [accessClosed, setAccessClosed] = useState(false);
  const attempt = useRef<string | null>(null);
  const controller = useRef<AbortController | null>(null);

  useEffect(() => {
    if (!enabled) {
      return;
    }
    const read = new AbortController();
    privateApiFetch(
      CUSTOMER_CLOSURE_BLOCKERS_PATH,
      parseClosurePreview,
      { signal: read.signal },
      { fresh: true },
    )
      .then((result) => {
        if (read.signal.aborted) {
          return;
        }
        setState(
          result.status === 200 && result.data
            ? { status: "ready", preview: result.data }
            : { status: "failed" },
        );
      })
      .catch(() => {
        if (!read.signal.aborted) {
          setState({ status: "failed" });
        }
      });
    return () => read.abort();
  }, [enabled, reads]);

  useEffect(() => () => controller.current?.abort(), []);
  useEffect(() => {
    if (!busy && !uncertain) {
      return;
    }
    const guard = (event: BeforeUnloadEvent) => event.preventDefault();
    window.addEventListener("beforeunload", guard);
    return () => window.removeEventListener("beforeunload", guard);
  }, [busy, uncertain]);

  function reload() {
    setState({ status: "loading" });
    setReads((count) => count + 1);
  }

  async function confirm(password: string) {
    if (controller.current || password.length === 0) {
      return;
    }
    attempt.current ??= JSON.stringify({ operationId: crypto.randomUUID(), password });
    const replaying = uncertain;
    const operation = new AbortController();
    controller.current = operation;
    setBusy(true);
    setProblem("");
    try {
      const result = await privateApiFetch(
        CUSTOMER_CLOSURE_PATH,
        parseClosureReceipt,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: attempt.current,
          signal: operation.signal,
        },
        { parseConflict: closureConflict, expectSessionLoss: replaying },
      );
      if (operation.signal.aborted) {
        return;
      }
      if (result.status === 401 && replaying) {
        attempt.current = null;
        setUncertain(false);
        setAccessClosed(true);
        return;
      }
      if (isUnconfirmedResult(result, replaying)) {
        throw new Error("Unconfirmed closure");
      }
      attempt.current = null;
      setUncertain(false);
      if (result.status === 200 && result.data) {
        setReceipt(result.data);
        return;
      }
      setProblem(result.message);
      if (result.code === "blocked") {
        reload();
      }
    } catch {
      if (!operation.signal.aborted) {
        setUncertain(true);
        setProblem(
          "The result is unconfirmed. Confirm again to reconcile it; it cannot be applied twice.",
        );
      }
    } finally {
      controller.current = null;
      if (!operation.signal.aborted) {
        setBusy(false);
      }
    }
  }

  return {
    state,
    busy,
    uncertain,
    problem,
    receipt,
    accessClosed,
    clearProblem: () => setProblem(""),
    reload,
    confirm,
  };
}
