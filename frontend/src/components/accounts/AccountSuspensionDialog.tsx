"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { Textarea } from "@/components/ui/Input";
import type { useAccountCommand } from "@/hooks/use-account-command";
import { emptySuspension, type AccountSuspensionDraft } from "@/lib/accounts/account-suspension";
import forms from "@/components/staff/StaffForms.module.css";
import styles from "./StaffAccounts.module.css";

// Suspending asks for a reason; lifting asks only about the two security choices. Both choices start off.
export function AccountSuspensionDialog({
  open,
  lifting,
  name,
  command,
  customer = false,
  onSubmit,
  onClose,
}: {
  open: boolean;
  lifting: boolean;
  name: string;
  command: ReturnType<typeof useAccountCommand>;
  // A customer's bookings and sales continue; only their sign-in stops.
  customer?: boolean;
  onSubmit: (draft: AccountSuspensionDraft) => Promise<boolean>;
  onClose: () => void;
}) {
  const [draft, setDraft] = useState<AccountSuspensionDraft>(emptySuspension);
  const change = (patch: Partial<AccountSuspensionDraft>) =>
    setDraft((current) => ({ ...current, ...patch }));
  const title = lifting ? "Lift suspension" : "Suspend account";
  const complete = lifting || draft.reason.trim().length > 0;
  let note =
    "Sign-in stops at once and every session ends. Their bookings and sales continue as normal.";
  if (lifting) {
    note = "They can sign in again as soon as you confirm.";
  } else if (!customer) {
    note =
      "Access stops at once and every session ends. Their work stays assigned until a manager reassigns it.";
  }

  return (
    <Dialog
      open={open}
      title={title}
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
              setDraft(emptySuspension());
              onClose();
            }
          });
        }}
      >
        <p className={styles.note}>{note}</p>
        {!lifting && (
          <Textarea
            label="Reason"
            value={draft.reason}
            maxLength={1000}
            required
            disabled={command.busy}
            onChange={(event) => change({ reason: event.target.value })}
          />
        )}
        <label className={forms.check}>
          <input
            type="checkbox"
            checked={draft.requirePasswordReset}
            disabled={command.busy}
            onChange={(event) => change({ requirePasswordReset: event.target.checked })}
          />
          Require password reset
        </label>
        <label className={forms.check}>
          <input
            type="checkbox"
            checked={draft.requireEmailVerification}
            disabled={command.busy}
            onChange={(event) => change({ requireEmailVerification: event.target.checked })}
          />
          Require email verification
        </label>
        <div className={styles.actions}>
          <Button type="button" variant="ghost" disabled={command.busy} onClick={onClose}>
            Cancel
          </Button>
          <Button
            type="submit"
            variant={lifting ? "primary" : "danger"}
            disabled={command.busy || !complete}
          >
            {command.busy ? "Saving" : title}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
