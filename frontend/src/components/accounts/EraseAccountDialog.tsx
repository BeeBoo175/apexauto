"use client";

import { useEffect, useState } from "react";
import { BlockerList, RetainedList } from "@/components/auth/ClosureLists";
import { ConfirmationDialog } from "@/components/ui/ConfirmationDialog";
import { Input, Textarea } from "@/components/ui/Input";
import { useToast } from "@/components/ui/Toast";
import { useAccountErasure } from "@/hooks/use-account-erasure";
import type { ErasureKind } from "@/lib/accounts/account-erasure";

const blockedIntro =
  "Erasing is blocked while these records are open. Nothing is cancelled or reassigned for you.";

// An Administrator's permanent erasure of one customer or employee: blockers first, then what stays, a reason for the
// audit trail and the Administrator's own password. The result is pseudonymized, so the dialog never says anonymous.
export function EraseAccountDialog({
  kind,
  accountId,
  name,
  open,
  onClose,
  onErased,
}: {
  kind: ErasureKind;
  accountId: string | null;
  name: string;
  open: boolean;
  onClose: () => void;
  onErased: () => void;
}) {
  const erasure = useAccountErasure(kind, accountId, open);
  const [reason, setReason] = useState("");
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
      error("Erasure details could not be loaded. Check again to continue.");
    }
  }, [open, state.status, error]);

  function close() {
    setReason("");
    setPassword("");
    erasure.clearProblem();
    if (receipt) {
      onErased();
    }
    erasure.reset();
    onClose();
  }

  if (receipt) {
    return (
      <ConfirmationDialog
        open={open}
        title="Account erased"
        confirmLabel="Done"
        cancelLabel="Close"
        locked
        onClose={close}
        onConfirm={close}
      >
        <p>
          Access ended and the personal details of {name} were removed.
          {receipt.fileRemovalPending
            ? " Their picture and uploaded files are still being removed."
            : ""}
        </p>
        <p>
          This is pseudonymized, not anonymous: some records keep details for the reasons below.
        </p>
        <RetainedList retained={receipt.retained} />
      </ConfirmationDialog>
    );
  }

  if (state.status !== "ready") {
    return (
      <ConfirmationDialog
        open={open}
        title="Erase account"
        confirmLabel="Check again"
        cancelLabel="Not now"
        busy={state.status === "loading"}
        onClose={close}
        onConfirm={erasure.reload}
      >
        <p>
          {state.status === "loading"
            ? "Checking the account."
            : "The account could not be checked."}
        </p>
      </ConfirmationDialog>
    );
  }

  if (state.preview.blockers.length > 0) {
    return (
      <ConfirmationDialog
        open={open}
        title="Erase account"
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
      title="Erase account"
      confirmLabel={erasure.uncertain ? "Confirm again" : "Erase permanently"}
      cancelLabel="Not now"
      danger
      busy={erasure.busy}
      locked={erasure.uncertain}
      onClose={close}
      onConfirm={() => erasure.confirm(reason, password)}
    >
      <p>
        <strong>This cannot be undone.</strong> {name} is signed out everywhere and nobody can
        restore the account. Their sign-in and contact details are removed from the account. Their
        name and the details a sale document needs are kept only on the records listed below.
      </p>
      <p>Not everything is erased. These records stay, for the reasons given:</p>
      <RetainedList retained={state.preview.retained} />
      <Textarea
        label="Reason"
        value={reason}
        maxLength={1000}
        required
        disabled={erasure.busy || erasure.uncertain}
        onChange={(event) => {
          erasure.clearProblem();
          setReason(event.target.value);
        }}
      />
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
