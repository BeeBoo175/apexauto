"use client";

import { useEffect, useState, type ReactNode } from "react";
import { ConfirmationDialog } from "@/components/ui/ConfirmationDialog";
import { Input } from "@/components/ui/Input";
import { useToast } from "@/components/ui/Toast";
import type { ErasurePreviewState } from "@/hooks/use-confirmed-erasure";
import type { ClosureReceipt } from "@/lib/accounts/customer-closure";
import { BlockerList, RetainedList } from "./ClosureLists";

interface ErasureControl {
  state: ErasurePreviewState;
  busy: boolean;
  uncertain: boolean;
  problem: string;
  receipt: ClosureReceipt | null;
  clearProblem: () => void;
  reload: () => void;
}

interface ReasonControl {
  disabled: boolean;
  clearProblem: () => void;
}

interface ErasureDialogProps {
  open: boolean;
  erasure: ErasureControl;
  title: string;
  checkingText: string;
  failedText: string;
  failedToast: string;
  blockedIntro?: string;
  warning: ReactNode;
  confirmLabel: string;
  // Set once the erasure is recorded, even when no receipt could be read back.
  finished?: boolean;
  finishedTitle: string;
  finishedCancelLabel: string;
  finishedBody: ReactNode;
  renderReason?: (control: ReasonControl) => ReactNode;
  onConfirm: (password: string) => void;
  onClose: () => void;
  onDone: () => void;
}

// The one confirmation flow for permanent erasure: receipt, loading or failed read, blockers, then what stays and
// the account holder's password.
export function ErasureDialog({
  open,
  erasure,
  title,
  checkingText,
  failedText,
  failedToast,
  blockedIntro,
  warning,
  confirmLabel,
  finished = false,
  finishedTitle,
  finishedCancelLabel,
  finishedBody,
  renderReason,
  onConfirm,
  onClose,
  onDone,
}: ErasureDialogProps) {
  const [password, setPassword] = useState("");
  const { error } = useToast();
  const { problem, state, receipt } = erasure;

  useEffect(() => {
    if (problem) {
      error(problem);
    }
  }, [problem, error]);

  useEffect(() => {
    if (open && state.status === "failed") {
      error(failedToast);
    }
  }, [open, state.status, error, failedToast]);

  function close() {
    setPassword("");
    erasure.clearProblem();
    onClose();
  }

  function done() {
    setPassword("");
    erasure.clearProblem();
    onDone();
  }

  if (receipt || finished) {
    return (
      <ConfirmationDialog
        open={open}
        title={finishedTitle}
        confirmLabel="Done"
        cancelLabel={finishedCancelLabel}
        locked
        onClose={done}
        onConfirm={done}
      >
        {finishedBody}
        {receipt && <RetainedList retained={receipt.retained} />}
      </ConfirmationDialog>
    );
  }

  if (state.status !== "ready") {
    return (
      <ConfirmationDialog
        open={open}
        title={title}
        confirmLabel="Check again"
        cancelLabel="Not now"
        busy={state.status === "loading"}
        onClose={close}
        onConfirm={erasure.reload}
      >
        <p>{state.status === "loading" ? checkingText : failedText}</p>
      </ConfirmationDialog>
    );
  }

  if (state.preview.blockers.length > 0) {
    return (
      <ConfirmationDialog
        open={open}
        title={title}
        confirmLabel="Check again"
        cancelLabel="Not now"
        onClose={close}
        onConfirm={erasure.reload}
      >
        <BlockerList blockers={state.preview.blockers} intro={blockedIntro} />
      </ConfirmationDialog>
    );
  }

  return (
    <ConfirmationDialog
      open={open}
      title={title}
      confirmLabel={erasure.uncertain ? "Confirm again" : confirmLabel}
      cancelLabel="Not now"
      danger
      busy={erasure.busy}
      locked={erasure.uncertain}
      onClose={close}
      onConfirm={() => onConfirm(password)}
    >
      {warning}
      <p>Not everything is erased. These records stay, for the reasons given:</p>
      <RetainedList retained={state.preview.retained} />
      {renderReason?.({
        disabled: erasure.busy || erasure.uncertain,
        clearProblem: erasure.clearProblem,
      })}
      <Input
        label="Your password"
        type="password"
        value={password}
        autoComplete="current-password"
        maxLength={128}
        disabled={erasure.busy || erasure.uncertain}
        aria-invalid={problem ? true : undefined}
        onChange={(event) => {
          erasure.clearProblem();
          setPassword(event.target.value);
        }}
      />
    </ConfirmationDialog>
  );
}
