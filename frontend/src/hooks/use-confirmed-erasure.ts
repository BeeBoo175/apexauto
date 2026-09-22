"use client";

import { useEffect, useRef, useState } from "react";
import { isUnconfirmedResult } from "@/hooks/use-api-command";
import type { ApiConflict } from "@/lib/api";
import { privateApiFetch } from "@/lib/api-browser";
import {
  parseClosureReceipt,
  type ClosurePreview,
  type ClosureReceipt,
} from "@/lib/accounts/customer-closure";

export type ErasurePreviewState =
  { status: "loading" } | { status: "failed" } | { status: "ready"; preview: ClosurePreview };

interface ConfirmedErasureOptions {
  enabled: boolean;
  previewPath: string | null;
  parsePreview: (value: unknown) => ClosurePreview | null;
  commandPath: string | null;
  parseConflict: (code: unknown) => ApiConflict | undefined;
  // A replay that meets a closed session means the first attempt had already happened.
  onSessionLost?: () => void;
}

// The workflow both account erasures share: the preview is read each time the dialog opens because a blocker may
// have been resolved or created since, and a confirmation the browser could not confirm keeps its exact body and
// operation id so replaying it can never apply the erasure twice.
export function useConfirmedErasure(options: ConfirmedErasureOptions) {
  const { enabled, previewPath, parsePreview, commandPath, parseConflict, onSessionLost } = options;
  const [state, setState] = useState<ErasurePreviewState>({ status: "loading" });
  const [reads, setReads] = useState(0);
  const [busy, setBusy] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  const [problem, setProblem] = useState("");
  const [receipt, setReceipt] = useState<ClosureReceipt | null>(null);
  const attempt = useRef<string | null>(null);
  const controller = useRef<AbortController | null>(null);

  useEffect(() => {
    if (!enabled || !previewPath) {
      return;
    }
    const read = new AbortController();
    privateApiFetch(previewPath, parsePreview, { signal: read.signal }, { fresh: true })
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
  }, [enabled, previewPath, parsePreview, reads]);

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

  async function confirm(fields: Record<string, string>) {
    if (controller.current || !commandPath) {
      return;
    }
    attempt.current ??= JSON.stringify({ operationId: crypto.randomUUID(), ...fields });
    const replaying = uncertain;
    const operation = new AbortController();
    controller.current = operation;
    setBusy(true);
    setProblem("");
    try {
      const result = await privateApiFetch(
        commandPath,
        parseClosureReceipt,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: attempt.current,
          signal: operation.signal,
        },
        { parseConflict, expectSessionLoss: onSessionLost !== undefined && replaying },
      );
      if (operation.signal.aborted) {
        return;
      }
      if (onSessionLost && result.status === 401 && replaying) {
        attempt.current = null;
        setUncertain(false);
        onSessionLost();
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
