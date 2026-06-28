"use client";

import { useState } from "react";
import { StaffRecordSummary } from "@/components/staff/StaffRecordSummary";
import { StaffWorkspaceTabs } from "@/components/staff/StaffWorkspaceTabs";
import { StaffStatusGroup } from "@/components/staff/StaffStatusGroup";
import { Avatar } from "@/components/ui/Avatar";
import { Button } from "@/components/ui/Button";
import { ConfirmationButton } from "@/components/ui/ConfirmationButton";
import { Dialog } from "@/components/ui/Dialog";
import { Input } from "@/components/ui/Input";
import { useAddressSection } from "@/hooks/use-address-section";
import type { useAccountCommand } from "@/hooks/use-account-command";
import { roleNames } from "@/lib/auth/actor";
import { formatMalaysianDateTime } from "@/lib/date-format";
import type { StaffAccountRow } from "@/lib/accounts/staff-accounts";
import { AccountSuspensionDialog } from "./AccountSuspensionDialog";
import { AccountHistory } from "./AccountHistory";
import { StaffAccountStatus } from "./StaffAccountStatus";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import styles from "./StaffAccounts.module.css";

const roleName = (role: string) => roleNames[role as keyof typeof roleNames] ?? role;

// One staff member or pending invitation. Invitations can be sent again, sent to a corrected address or
// revoked; accounts are read-only here until their own administration actions arrive.
export function StaffAccountDrawer({
  row,
  selfId,
  command,
  onClose,
  onChanged,
}: {
  row: StaffAccountRow | null;
  selfId: string;
  command: ReturnType<typeof useAccountCommand>;
  onClose: () => void;
  onChanged: () => void;
}) {
  const [editing, setEditing] = useState(false);
  const [section, chooseSection] = useAddressSection<"details" | "history">(["details", "history"]);
  // History is read only once its tab has been opened.
  const [historyOpened, setHistoryOpened] = useState(section === "history");
  const [suspension, setSuspension] = useState<"suspend" | "lift" | null>(null);
  const [email, setEmail] = useState("");
  const invitation = row?.status === "Invited" || row?.status === "InvitationExpired";

  function close() {
    setEditing(false);
    onClose();
  }

  async function run(action: Promise<boolean>) {
    const done = await action;
    if (done) {
      setEditing(false);
      onChanged();
    }
    return done;
  }

  // Suspension applies to an account that is active (and not the administrator's own) or already suspended.
  const suspendable = row?.status === "Active" && row.id !== selfId;
  const suspended = row?.status === "Suspended";

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
          <StaffWorkspaceTabs
            label="Account sections"
            tabs={[
              { value: "details", label: "Details", available: true },
              { value: "history", label: "History", available: true },
            ]}
            value={section}
            onChange={(next) => {
              chooseSection(next);
              setHistoryOpened((opened) => opened || next === "history");
            }}
          />
          <div className={styles.panel} hidden={section !== "details"}>
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
              {(suspendable || suspended) && (
                <section className={styles.section} aria-label="Access">
                  <div className={workbench.sectionHeader}>
                    <h2 className={workbench.sectionTitle}>Access</h2>
                  </div>
                  <div className={styles.actions}>
                    <Button
                      variant={suspended ? "secondary" : "danger"}
                      size="compact"
                      disabled={command.busy}
                      onClick={() => setSuspension(suspended ? "lift" : "suspend")}
                    >
                      {suspended ? "Lift suspension" : "Suspend account"}
                    </Button>
                  </div>
                </section>
              )}
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
                        <Button
                          type="submit"
                          size="compact"
                          disabled={command.busy || !email.trim()}
                        >
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
          {historyOpened && (
            <div className={styles.panel} hidden={section !== "history"}>
              <div className={workbench.body}>
                <AccountHistory key={row.id} accountId={row.id} />
              </div>
            </div>
          )}
        </div>
      )}
      <AccountSuspensionDialog
        open={suspension !== null}
        lifting={suspension === "lift"}
        name={row?.name ?? ""}
        command={command}
        onSubmit={(draft) =>
          row
            ? run(
                suspension === "lift"
                  ? command.lift(row.id, draft)
                  : command.suspend(row.id, draft),
              )
            : Promise.resolve(false)
        }
        onClose={() => setSuspension(null)}
      />
    </Dialog>
  );
}
