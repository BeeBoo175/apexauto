"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { Input, Select } from "@/components/ui/Input";
import type { useAccountCommand } from "@/hooks/use-account-command";
import type { Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import { roleNames } from "@/lib/auth/actor";
import { staffAccountRoles } from "@/lib/accounts/staff-accounts";
import {
  emptyInvitation,
  isBranchRole,
  type StaffInvitationDraft,
} from "@/lib/accounts/staff-invitations";
import styles from "./StaffAccounts.module.css";

// The Administrator names the person, role and branch; the person chooses their own password and username.
export function StaffInviteDialog({
  open,
  branches,
  command,
  onClose,
  onInvited,
}: {
  open: boolean;
  branches: Page<Branch> | null;
  command: ReturnType<typeof useAccountCommand>;
  onClose: () => void;
  onInvited: () => void;
}) {
  const [draft, setDraft] = useState<StaffInvitationDraft>(emptyInvitation);
  const change = (patch: Partial<StaffInvitationDraft>) =>
    setDraft((current) => ({ ...current, ...patch }));
  const needsBranch = isBranchRole(draft.role);
  const complete =
    draft.name.trim() &&
    draft.email.trim() &&
    draft.mobile.trim() &&
    (!needsBranch || draft.branchId);

  return (
    <Dialog open={open} title="Invite staff" onClose={onClose} closeDisabled={command.busy}>
      <form
        className={styles.form}
        onSubmit={(event) => {
          event.preventDefault();
          void command
            .invite({ ...draft, branchId: needsBranch ? draft.branchId : null })
            .then((sent) => {
              if (sent) {
                setDraft(emptyInvitation());
                onInvited();
                onClose();
              }
            });
        }}
      >
        <Input
          label="Full name"
          value={draft.name}
          maxLength={200}
          required
          disabled={command.busy}
          onChange={(event) => change({ name: event.target.value })}
        />
        <Input
          label="Email"
          type="email"
          value={draft.email}
          maxLength={254}
          required
          disabled={command.busy}
          onChange={(event) => change({ email: event.target.value })}
        />
        <Input
          label="Work mobile"
          value={draft.mobile}
          maxLength={30}
          required
          disabled={command.busy}
          onChange={(event) => change({ mobile: event.target.value })}
        />
        <Select
          label="Role"
          value={draft.role}
          disabled={command.busy}
          onChange={(event) => change({ role: event.target.value, branchId: "" })}
        >
          {staffAccountRoles.map((role) => (
            <option key={role} value={role}>
              {roleNames[role]}
            </option>
          ))}
        </Select>
        {needsBranch && (
          <Select
            label="Branch"
            value={draft.branchId}
            required
            disabled={command.busy}
            onChange={(event) => change({ branchId: event.target.value })}
          >
            <option value="">Choose a branch</option>
            {branches?.items.map((branch) => (
              <option key={branch.id} value={branch.id}>
                {branch.name}
              </option>
            ))}
          </Select>
        )}
        <p className={styles.note}>
          They receive a link valid for 72 hours to choose a username and password.
        </p>
        <div className={styles.actions}>
          <Button type="button" variant="ghost" disabled={command.busy} onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" disabled={command.busy || !complete}>
            {command.busy ? "Sending invitation" : "Send invitation"}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
