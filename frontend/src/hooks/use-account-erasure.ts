"use client";

import { useEffect, useRef, useState } from "react";
import { isUnconfirmedResult } from "@/hooks/use-api-command";
import { privateApiFetch } from "@/lib/api-browser";
import {
  erasureConflict,
  erasurePath,
  erasurePreviewPath,
  parseErasurePreview,
  type ErasureKind,
} from "@/lib/accounts/account-erasure";
import {
  parseClosureReceipt,
  type ClosurePreview,
  type ClosureReceipt,
} from "@/lib/accounts/customer-closure";

type PreviewState =
  { status: "loading" } | { status: "failed" } | { status: "ready"; preview: ClosurePreview };

// An Administrator's erasure of a customer or an employee. The preview is read each time the dialog opens because a
// blocker may have been resolved or created since. A confirmation the browser could not confirm keeps its exact body
// and operation id: replaying it returns the stored receipt, so it can never be applied twice.
export function useAccountErasure(kind: ErasureKind, id: string | null, enabled: boolean) {
  const [state, setState] = useState<PreviewState>({ status: "loading" });
  const [reads, setReads] = useState(0);
  const [busy, setBusy] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  const [problem, setProblem] = useState("");
  const [receipt, setReceipt] = useState<ClosureReceipt | null>(null);
  const attempt = useRef<string | null>(null);
  const controller = useRef<AbortController | null>(null);

  useEffect(() => {
    if (!enabled || !id) {
      return;
    }
    const read = new AbortController();
    privateApiFetch(
      erasurePreviewPath(kind, id),
      (value) => parseErasurePreview(kind, value),
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
  }, [enabled, id, kind, reads]);

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

  // A different account starts from a clean state.
  function reset() {
    attempt.current = null;
    setUncertain(false);
    setProblem("");
    setReceipt(null);
    setState({ status: "loading" });
  }

  async function confirm(reason: string, password: string) {
    if (controller.current || !id || reason.trim().length === 0 || password.length === 0) {
      return;
    }
    attempt.current ??= JSON.stringify({ operationId: crypto.randomUUID(), reason, password });
    const replaying = uncertain;
    const operation = new AbortController();
    controller.current = operation;
    setBusy(true);
    setProblem("");
    try {
      const result = await privateApiFetch(
        erasurePath(kind, id),
        parseClosureReceipt,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: attempt.current,
          signal: operation.signal,
        },
        { parseConflict: erasureConflict },
      );
      if (operation.signal.aborted) {
        return;
      }
      if (isUnconfirmedResult(result, replaying)) {
        throw new Error("Unconfirmed erasure");
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
    clearProblem: () => setProblem(""),
    reload,
    reset,
    confirm,
  };
}
