"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { Input, Textarea } from "@/components/ui/Input";
import type { useAccountCommand } from "@/hooks/use-account-command";
import {
  isEdited,
  startEdit,
  type AccountEditDraft,
  type AccountEditSubject,
} from "@/lib/accounts/account-edit";
import forms from "@/components/staff/StaffForms.module.css";
import styles from "./StaffAccounts.module.css";

// An Administrator corrects recorded details with a reason. The two security choices appear only when the email
// changes, which applies at once and ends the person's sessions.
export function AccountEditDialog({
  open,
  subject,
  command,
  onSubmit,
  onClose,
}: {
  open: boolean;
  subject: AccountEditSubject | null;
  command: ReturnType<typeof useAccountCommand>;
  onSubmit: (draft: AccountEditDraft) => Promise<boolean>;
  onClose: () => void;
}) {
  return (
    <Dialog
      open={open && subject !== null}
      title="Edit details"
      description={subject?.name}
      onClose={onClose}
      closeDisabled={command.busy}
    >
      {open && subject && (
        <AccountEditForm
          subject={subject}
          command={command}
          onSubmit={onSubmit}
          onClose={onClose}
        />
      )}
    </Dialog>
  );
}

function AccountEditForm({
  subject,
  command,
  onSubmit,
  onClose,
}: {
  subject: AccountEditSubject;
  command: ReturnType<typeof useAccountCommand>;
  onSubmit: (draft: AccountEditDraft) => Promise<boolean>;
  onClose: () => void;
}) {
  const [draft, setDraft] = useState<AccountEditDraft>(() => startEdit(subject));
  const change = (patch: Partial<AccountEditDraft>) =>
    setDraft((current) => ({ ...current, ...patch }));
  const emailChanged = draft.email.trim() !== subject.email;
  const complete =
    draft.reason.trim().length > 0 &&
    draft.name.trim().length > 0 &&
    draft.mobile.trim().length > 0 &&
    draft.email.trim().length > 0 &&
    (draft.address === null || draft.address.trim().length > 0) &&
    isEdited(draft, subject);

  return (
    <form
      className={styles.form}
      onSubmit={(event) => {
        event.preventDefault();
        void onSubmit(draft).then((done) => {
          if (done) {
            onClose();
          }
        });
      }}
    >
      <Input
        label={draft.address === null ? "Name" : "Legal name"}
        value={draft.name}
        maxLength={200}
        required
        disabled={command.busy}
        onChange={(event) => change({ name: event.target.value })}
      />
      <Input
        label={draft.address === null ? "Work mobile" : "Mobile"}
        value={draft.mobile}
        maxLength={30}
        required
        disabled={command.busy}
        onChange={(event) => change({ mobile: event.target.value })}
      />
      {draft.address !== null && (
        <Textarea
          label="Address"
          value={draft.address}
          maxLength={1000}
          required
          disabled={command.busy}
          onChange={(event) => change({ address: event.target.value })}
        />
      )}
      <Input
        label="Email"
        type="email"
        value={draft.email}
        maxLength={254}
        required
        disabled={command.busy}
        onChange={(event) => change({ email: event.target.value })}
      />
      {emailChanged && (
        <>
          <p className={styles.note}>
            The new address applies at once, every session ends and unused reset links stop working.
          </p>
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
        </>
      )}
      <Textarea
        label="Reason"
        value={draft.reason}
        maxLength={1000}
        required
        disabled={command.busy}
        onChange={(event) => change({ reason: event.target.value })}
      />
      <div className={styles.actions}>
        <Button type="button" variant="ghost" disabled={command.busy} onClick={onClose}>
          Cancel
        </Button>
        <Button type="submit" disabled={command.busy || !complete}>
          {command.busy ? "Saving" : "Save changes"}
        </Button>
      </div>
    </form>
  );
}
