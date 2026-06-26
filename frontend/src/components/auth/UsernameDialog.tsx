"use client";

import { useState } from "react";
import { ConfirmationDialog } from "@/components/ui/ConfirmationDialog";
import { Input } from "@/components/ui/Input";
import type { useAccountUsername } from "@/hooks/use-account-username";

// The username is display-only: sign-in and recovery stay on email.
export function UsernameDialog({
  account,
  open,
  onClose,
}: {
  account: ReturnType<typeof useAccountUsername>;
  open: boolean;
  onClose: () => void;
}) {
  const [draft, setDraft] = useState(account.username);

  function close() {
    account.clearProblem();
    setDraft(account.username);
    onClose();
  }

  return (
    <ConfirmationDialog
      open={open}
      title="Change username"
      confirmLabel="Save username"
      cancelLabel="Not now"
      busy={account.busy}
      onClose={close}
      onConfirm={async () => {
        if (await account.change(draft)) {
          onClose();
        }
      }}
    >
      <p>Your username is shown to colleagues. You still sign in with your email.</p>
      <Input
        label="Username"
        value={draft}
        autoComplete="off"
        maxLength={30}
        disabled={account.busy}
        aria-invalid={account.problem ? true : undefined}
        aria-describedby={account.problem ? "username-problem" : undefined}
        onChange={(event) => {
          account.clearProblem();
          setDraft(event.target.value);
        }}
      />
      {account.problem && (
        <p id="username-problem" role="alert">
          {account.problem}
        </p>
      )}
    </ConfirmationDialog>
  );
}
