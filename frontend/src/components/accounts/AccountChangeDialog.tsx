"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { Select, Textarea } from "@/components/ui/Input";
import { useAccountBlockers } from "@/hooks/use-account-blockers";
import type { useAccountCommand } from "@/hooks/use-account-command";
import type { Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import {
  emptyAccountChange,
  type StaffAccountChangeDraft,
} from "@/lib/accounts/account-administration";
import type { StaffAccountRow } from "@/lib/accounts/staff-accounts";
import styles from "./StaffAccounts.module.css";

// Deactivation and a branch transfer share one flow: the open work the employee owns is read first and blocks
// the change while any remains; otherwise a reason (and, for a transfer, the branch) confirms it.
export function AccountChangeDialog({
  row,
  kind,
  branches,
  command,
  onSubmit,
  onClose,
}: {
  row: StaffAccountRow | null;
  kind: "deactivate" | "transfer" | null;
  branches: Page<Branch> | null;
  command: ReturnType<typeof useAccountCommand>;
  onSubmit: (draft: StaffAccountChangeDraft) => Promise<boolean>;
  onClose: () => void;
}) {
  const [draft, setDraft] = useState<StaffAccountChangeDraft>(emptyAccountChange);
  const open = row !== null && kind !== null;
  const read = useAccountBlockers(row?.id ?? "", open);
  const transfer = kind === "transfer";
  const title = transfer ? "Transfer to another branch" : "Deactivate account";
  const choices = branches?.items.filter((branch) => branch.id !== row?.branchId) ?? [];
  const blocked = (read.blockers?.length ?? 0) > 0;
  const ready =
    !read.loading &&
    !read.failed &&
    !blocked &&
    draft.reason.trim().length > 0 &&
    (!transfer || draft.branchId !== "");

  return (
    <Dialog
      open={open}
      title={title}
      description={row?.name}
      onClose={onClose}
      closeDisabled={command.busy}
    >
      <form
        className={styles.form}
        onSubmit={(event) => {
          event.preventDefault();
          void onSubmit(draft).then((done) => {
            if (done) {
              setDraft(emptyAccountChange());
              onClose();
            }
          });
        }}
      >
        {read.loading && <p className={styles.note}>Checking the work this employee still owns…</p>}
        {read.failed && (
          <p role="alert" className={styles.note}>
            The open work could not be read. Reload the page to try again.
          </p>
        )}
        {blocked && read.blockers && (
          <div role="alert" className={styles.blockers}>
            <p className={styles.note}>
              This employee still owns {read.blockers.length} open{" "}
              {read.blockers.length === 1 ? "record" : "records"}. Their branch manager resolves
              them first; nothing here links to the records.
            </p>
            <ul className={styles.blockerList}>
              {read.blockers.map((item) => (
                <li key={`${item.type}:${item.reference}`}>
                  <strong>{item.type}</strong> {item.reference} · {item.status}
                  <span>
                    {item.branchName} · {item.managers}
                  </span>
                </li>
              ))}
            </ul>
          </div>
        )}
        {transfer && (
          <Select
            label="New branch"
            value={draft.branchId}
            required
            disabled={command.busy}
            onChange={(event) =>
              setDraft((current) => ({ ...current, branchId: event.target.value }))
            }
          >
            <option value="">Choose a branch</option>
            {choices.map((branch) => (
              <option key={branch.id} value={branch.id}>
                {branch.name}
              </option>
            ))}
          </Select>
        )}
        <Textarea
          label="Reason"
          value={draft.reason}
          maxLength={1000}
          required
          disabled={command.busy}
          onChange={(event) => setDraft((current) => ({ ...current, reason: event.target.value }))}
        />
        <p className={styles.note}>
          {transfer
            ? "Access changes at once and sessions continue. Past work keeps its original branch."
            : "Access stops at once. The account and its history stay, and it can be reactivated."}
        </p>
        <div className={styles.actions}>
          <Button type="button" variant="ghost" disabled={command.busy} onClick={onClose}>
            Cancel
          </Button>
          <Button
            type="submit"
            variant={transfer ? "primary" : "danger"}
            disabled={command.busy || !ready}
          >
            {command.busy ? "Saving" : title}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
