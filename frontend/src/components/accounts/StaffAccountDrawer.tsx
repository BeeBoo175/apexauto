"use client";

import { useState } from "react";
import { StaffRecordSummary } from "@/components/staff/StaffRecordSummary";
import { StaffStatusGroup } from "@/components/staff/StaffStatusGroup";
import { StaffWorkspaceFooter } from "@/components/staff/StaffWorkspaceFooter";
import { StaffWorkspaceTabs } from "@/components/staff/StaffWorkspaceTabs";
import { Avatar } from "@/components/ui/Avatar";
import { Button } from "@/components/ui/Button";
import { ConfirmationButton } from "@/components/ui/ConfirmationButton";
import { Dialog } from "@/components/ui/Dialog";
import { Input } from "@/components/ui/Input";
import { useAddressSection } from "@/hooks/use-address-section";
import type { useAccountCommand } from "@/hooks/use-account-command";
import type { Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import { roleNames } from "@/lib/auth/actor";
import { formatMalaysianDateTime } from "@/lib/date-format";
import type { StaffAccountRow } from "@/lib/accounts/staff-accounts";
import { AccountChangeDialog } from "./AccountChangeDialog";
import { AccountEditDialog } from "./AccountEditDialog";
import { AccountHistory } from "./AccountHistory";
import { AccountSuspensionDialog } from "./AccountSuspensionDialog";
import { StaffAccountStatus } from "./StaffAccountStatus";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import styles from "./StaffAccounts.module.css";

const roleName = (role: string) => roleNames[role as keyof typeof roleNames] ?? role;

type Section = "details" | "history";

// One staff member or pending invitation, laid out like the other drawers: the summary band, a scrolling
// content area under Details and History tabs, and a footer carrying the actions the Administrator may take.
export function StaffAccountDrawer({
  row,
  selfId,
  branches,
  command,
  onClose,
  onChanged,
}: {
  row: StaffAccountRow | null;
  selfId: string;
  branches: Page<Branch> | null;
  command: ReturnType<typeof useAccountCommand>;
  onClose: () => void;
  onChanged: () => void;
}) {
  const [section, chooseSection] = useAddressSection<Section>(["details", "history"]);
  // History is read only once its tab has been opened.
  const [historyOpened, setHistoryOpened] = useState(section === "history");
  const [change, setChange] = useState<"deactivate" | "transfer" | null>(null);
  const [suspension, setSuspension] = useState<"suspend" | "lift" | null>(null);
  const [editing, setEditing] = useState(false);
  const [changingEmail, setChangingEmail] = useState(false);
  const [email, setEmail] = useState("");
  const invitation = row?.status === "Invited" || row?.status === "InvitationExpired";
  // Suspension applies to an active account that is not the administrator's own, or to one already suspended.
  const suspendable = row?.status === "Active" && row.id !== selfId;
  const suspended = row?.status === "Suspended";
  const deactivated = row?.status === "Deactivated";
  const self = row?.id === selfId;
  const branchScoped = row?.branchId != null;

  async function run(action: Promise<boolean>) {
    const done = await action;
    if (done) {
      onChanged();
    }
    return done;
  }

  let footerNote = "";
  if (invitation) {
    footerNote = "Manage this invite.";
  } else if (suspendable) {
    footerNote = "Ends all sessions.";
  } else if (suspended) {
    footerNote = "Restores sign-in.";
  } else if (deactivated) {
    footerNote = "Restores role and branch, not past work.";
  }
  if (!invitation && !self && !deactivated && !suspendable && !suspended) {
    footerNote = "";
  }

  return (
    <Dialog
      open={Boolean(row)}
      variant="drawer"
      density="compact"
      title={row?.name ?? "Staff account"}
      onClose={onClose}
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
            <div className={`${workbench.body} ${styles.content}`}>
              <section className={styles.section} aria-label="Contact">
                <div className={workbench.sectionHeader}>
                  <h2 className={workbench.sectionTitle}>Contact</h2>
                  {!invitation && !self && (
                    <Button
                      variant="secondary"
                      size="compact"
                      disabled={command.busy}
                      onClick={() => setEditing(true)}
                    >
                      Edit details
                    </Button>
                  )}
                </div>
                <dl className={styles.details}>
                  <dt>Email</dt>
                  <dd>{row.email}</dd>
                  <dt>Work mobile</dt>
                  <dd>{row.mobile}</dd>
                </dl>
              </section>
            </div>
          </div>
          {historyOpened && (
            <div className={styles.panel} hidden={section !== "history"}>
              <div className={`${workbench.body} ${styles.content}`}>
                <AccountHistory key={row.id} accountId={row.id} />
              </div>
            </div>
          )}
          {footerNote && (
            <StaffWorkspaceFooter
              title={invitation ? "Invitation" : "Access"}
              description={footerNote}
            >
              {invitation && (
                <>
                  <ConfirmationButton
                    variant="secondary"
                    size="compact"
                    disabled={command.busy}
                    confirmationTitle="Revoke this invitation?"
                    message="The link stops working and the email is released. Invite again to correct a wrong role."
                    onConfirm={() => run(command.revoke(row.id))}
                  >
                    Revoke
                  </ConfirmationButton>
                  <Button
                    variant="secondary"
                    size="compact"
                    disabled={command.busy}
                    onClick={() => {
                      setEmail(row.email);
                      setChangingEmail(true);
                    }}
                  >
                    Change email
                  </Button>
                  <Button
                    size="compact"
                    disabled={command.busy}
                    onClick={() => void run(command.resend(row.id))}
                  >
                    Resend invitation
                  </Button>
                </>
              )}
              {deactivated && (
                <ConfirmationButton
                  size="compact"
                  disabled={command.busy}
                  confirmationTitle="Reactivate this account?"
                  message="They keep their role and branch and can sign in again. Former work is not restored."
                  onConfirm={() => run(command.reactivate(row.id))}
                >
                  Reactivate
                </ConfirmationButton>
              )}
              {!invitation && !self && !deactivated && (
                <>
                  <Button
                    variant="secondary"
                    size="compact"
                    disabled={command.busy}
                    onClick={() => setChange("deactivate")}
                  >
                    Deactivate
                  </Button>
                  {branchScoped && row.status === "Active" && (
                    <Button
                      variant="secondary"
                      size="compact"
                      disabled={command.busy}
                      onClick={() => setChange("transfer")}
                    >
                      Transfer
                    </Button>
                  )}
                </>
              )}
              {(suspendable || suspended) && (
                <Button
                  variant={suspended ? "primary" : "danger"}
                  size="compact"
                  disabled={command.busy}
                  onClick={() => setSuspension(suspended ? "lift" : "suspend")}
                >
                  {suspended ? "Lift suspension" : "Suspend account"}
                </Button>
              )}
            </StaffWorkspaceFooter>
          )}
        </div>
      )}
      <AccountChangeDialog
        row={row}
        kind={change}
        branches={branches}
        command={command}
        onSubmit={(draft) =>
          row
            ? run(
                change === "transfer"
                  ? command.transfer(row.id, draft)
                  : command.deactivate(row.id, draft),
              )
            : Promise.resolve(false)
        }
        onClose={() => setChange(null)}
      />
      <AccountEditDialog
        open={editing}
        subject={
          row ? { name: row.name, mobile: row.mobile, address: null, email: row.email } : null
        }
        command={command}
        onSubmit={(draft) => (row ? run(command.edit(row.id, draft)) : Promise.resolve(false))}
        onClose={() => setEditing(false)}
      />
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
      <Dialog
        open={changingEmail}
        title="Change invitation email"
        description={row?.name}
        onClose={() => setChangingEmail(false)}
        closeDisabled={command.busy}
      >
        <form
          className={styles.form}
          onSubmit={(event) => {
            event.preventDefault();
            if (row) {
              void run(command.changeEmail(row.id, email.trim())).then((done) => {
                if (done) {
                  setChangingEmail(false);
                }
              });
            }
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
          <p className={styles.note}>The old link stops working and a new one is sent.</p>
          <div className={styles.actions}>
            <Button
              type="button"
              variant="ghost"
              disabled={command.busy}
              onClick={() => setChangingEmail(false)}
            >
              Cancel
            </Button>
            <Button type="submit" disabled={command.busy || !email.trim()}>
              Save and resend
            </Button>
          </div>
        </form>
      </Dialog>
    </Dialog>
  );
}
