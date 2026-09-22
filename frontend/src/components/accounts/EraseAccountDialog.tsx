"use client";

import { useState } from "react";
import { ErasureDialog } from "@/components/auth/ErasureDialog";
import { Textarea } from "@/components/ui/Input";
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
  const { receipt } = erasure;

  function close() {
    setReason("");
    if (receipt) {
      onErased();
    }
    erasure.reset();
    onClose();
  }

  return (
    <ErasureDialog
      open={open}
      erasure={erasure}
      title="Erase account"
      checkingText="Checking the account."
      failedText="The account could not be checked."
      failedToast="Erasure details could not be loaded. Check again to continue."
      blockedIntro={blockedIntro}
      warning={
        <p>
          <strong>This cannot be undone.</strong> {name} is signed out everywhere and nobody can
          restore the account. Their sign-in and contact details are removed from the account. Their
          name and the details a sale document needs are kept only on the records listed below.
        </p>
      }
      confirmLabel="Erase permanently"
      finishedTitle="Account erased"
      finishedCancelLabel="Close"
      finishedBody={
        <>
          <p>
            Access ended and the personal details of {name} were removed.
            {receipt?.fileRemovalPending
              ? " Their picture and uploaded files are still being removed."
              : ""}
          </p>
          <p>
            This is pseudonymized, not anonymous: some records keep details for the reasons below.
          </p>
        </>
      }
      renderReason={({ disabled, clearProblem }) => (
        <Textarea
          label="Reason"
          value={reason}
          maxLength={1000}
          required
          disabled={disabled}
          onChange={(event) => {
            clearProblem();
            setReason(event.target.value);
          }}
        />
      )}
      onConfirm={(password) => erasure.confirm(reason, password)}
      onClose={close}
      onDone={close}
    />
  );
}
