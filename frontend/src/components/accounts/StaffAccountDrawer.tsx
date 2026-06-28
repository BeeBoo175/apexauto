"use client";

import { useState } from "react";
import { StaffRecordSummary } from "@/components/staff/StaffRecordSummary";
import { StaffStatusGroup } from "@/components/staff/StaffStatusGroup";
import { Avatar } from "@/components/ui/Avatar";
import { Button } from "@/components/ui/Button";
import { ConfirmationButton } from "@/components/ui/ConfirmationButton";
import { Dialog } from "@/components/ui/Dialog";
import { Input } from "@/components/ui/Input";
import type { useStaffInvitationCommand } from "@/hooks/use-staff-invitation-command";
import { roleNames } from "@/lib/auth/actor";
import { formatMalaysianDateTime } from "@/lib/date-format";
import type { StaffAccountRow } from "@/lib/accounts/staff-accounts";
import { StaffAccountStatus } from "./StaffAccountStatus";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import styles from "./StaffAccounts.module.css";

const roleName = (role: string) => roleNames[role as keyof typeof roleNames] ?? role;

// One staff member or pending invitation. Invitations can be sent again, sent to a corrected address or
// revoked; accounts are read-only here until their own administration actions arrive.
export function StaffAccountDrawer({
  row,
  command,
  onClose,
  onChanged,
}: {
  row: StaffAccountRow | null;
  command: ReturnType<typeof useStaffInvitationCommand>;
  onClose: () => void;
  onChanged: () => void;
}) {
  const [editing, setEditing] = useState(false);
  const [email, setEmail] = useState("");
  const invitation = row?.status === "Invited" || row?.status === "InvitationExpired";

  function close() {
    setEditing(false);
    onClose();
  }

  async function run(action: Promise<boolean>) {
    if (await action) {
      setEditing(false);
      onChanged();
    }
  }

  return (
    <Dialog
      open={Boolean(row)}
      variant="drawer"
      density="compact"
      title={row?.name ?? "Staff account"}
      onClose={close}
      closeDisabled={command.busy}
    >
      {row && (
        <div className={workbench.workspace}>
          <StaffRecordSummary
            ariaLabel="Role, branch and status"
            label={row.branchName ?? "All branches"}
            value={roleName(row.role)}
            lead={<Avatar accountId={row.id} name={row.name} pictureId={row.pictureId} size="lg" />}
            status={
              <StaffStatusGroup label="Account status">
                <StaffAccountStatus status={row.status} />
              </StaffStatusGroup>
            }
            context={
              row.invitationExpiresAt && (
                <span>
                  {row.status === "Invited" ? "Link expires " : "Link expired "}
                  {formatMalaysianDateTime(row.invitationExpiresAt)}
                </span>
              )
            }
          />
          <div className={workbench.body}>
            <section className={styles.section} aria-label="Contact and role">
              <div className={workbench.sectionHeader}>
                <h2 className={workbench.sectionTitle}>Details</h2>
              </div>
              <dl className={styles.details}>
                <dt>Email</dt>
                <dd>{row.email}</dd>
                <dt>Work mobile</dt>
                <dd>{row.mobile}</dd>
                <dt>Role</dt>
                <dd>{roleName(row.role)}</dd>
                <dt>Branch</dt>
                <dd>{row.branchName ?? "All branches"}</dd>
              </dl>
            </section>
            {invitation && (
              <section className={styles.section} aria-label="Invitation actions">
                <div className={workbench.sectionHeader}>
                  <h2 className={workbench.sectionTitle}>Invitation</h2>
                </div>
                {editing ? (
                  <form
                    className={styles.form}
                    onSubmit={(event) => {
                      event.preventDefault();
                      void run(command.changeEmail(row.id, email.trim()));
                    }}
                  >
                    <Input
                      label="Corrected email"
                      type="email"
                      value={email}
                      maxLength={254}
                      required
                      disabled={command.busy}
                      onChange={(event) => setEmail(event.target.value)}
                    />
                    <div className={styles.actions}>
                      <Button
                        type="button"
                        variant="ghost"
                        size="compact"
                        disabled={command.busy}
                        onClick={() => setEditing(false)}
                      >
                        Cancel
                      </Button>
                      <Button type="submit" size="compact" disabled={command.busy || !email.trim()}>
                        Save and resend
                      </Button>
                    </div>
                  </form>
                ) : (
                  <div className={styles.actions}>
                    <Button
                      variant="secondary"
                      size="compact"
                      disabled={command.busy}
                      onClick={() => void run(command.resend(row.id))}
                    >
                      Resend invitation
                    </Button>
                    <Button
                      variant="secondary"
                      size="compact"
                      disabled={command.busy}
                      onClick={() => {
                        setEmail(row.email);
                        setEditing(true);
                      }}
                    >
                      Change email
                    </Button>
                    <ConfirmationButton
                      variant="danger"
                      size="compact"
                      disabled={command.busy}
                      confirmationTitle="Revoke this invitation?"
                      message="The link stops working and the email is released. Invite again to correct a wrong role."
                      onConfirm={() => run(command.revoke(row.id))}
                    >
                      Revoke
                    </ConfirmationButton>
                  </div>
                )}
              </section>
            )}
          </div>
        </div>
      )}
    </Dialog>
  );
}
