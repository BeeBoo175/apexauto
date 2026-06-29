"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { Textarea } from "@/components/ui/Input";
import type { useAccountCommand } from "@/hooks/use-account-command";
import {
  emptySupport,
  type CustomerSupportAction,
  type CustomerSupportDraft,
} from "@/lib/accounts/customer-support";
import styles from "./StaffAccounts.module.css";

const copy: Record<CustomerSupportAction, { title: string; note: string }> = {
  "nric-reveal": {
    title: "Reveal NRIC",
    note: "The full number is shown in this account until you close it. Revealing is recorded with your reason.",
  },
  "session-revocation": {
    title: "End all sessions",
    note: "Every signed-in device is signed out at once. The customer can sign in again.",
  },
  "password-reset": {
    title: "Send password reset",
    note: "The customer receives the usual reset link by email. Their current password keeps working until they use it.",
  },
  "verification-resend": {
    title: "Resend verification",
    note: "The customer receives the verification link again, within its resend limits.",
  },
};

// Every customer support action asks for a reason, which the account history records with who did it.
export function CustomerSupportDialog({
  open,
  action,
  name,
  command,
  onSubmit,
  onClose,
}: {
  open: boolean;
  action: CustomerSupportAction | null;
  name: string;
  command: ReturnType<typeof useAccountCommand>;
  onSubmit: (draft: CustomerSupportDraft) => Promise<boolean>;
  onClose: () => void;
}) {
  const [draft, setDraft] = useState<CustomerSupportDraft>(emptySupport);
  const text = action ? copy[action] : null;

  return (
    <Dialog
      open={open}
      title={text?.title ?? "Support"}
      description={name}
      onClose={onClose}
      closeDisabled={command.busy}
    >
      <form
        className={styles.form}
        onSubmit={(event) => {
          event.preventDefault();
          void onSubmit(draft).then((done) => {
            if (done) {
              setDraft(emptySupport());
              onClose();
            }
          });
        }}
      >
        <p className={styles.note}>{text?.note}</p>
        <Textarea
          label="Reason"
          value={draft.reason}
          maxLength={1000}
          required
          disabled={command.busy}
          onChange={(event) => setDraft((current) => ({ ...current, reason: event.target.value }))}
        />
        <div className={styles.actions}>
          <Button type="button" variant="ghost" disabled={command.busy} onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" disabled={command.busy || draft.reason.trim().length === 0}>
            {command.busy ? "Saving" : (text?.title ?? "Confirm")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
