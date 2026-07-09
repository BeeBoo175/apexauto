"use client";

import { useEffect, useState } from "react";
import { StaffRecordSummary } from "@/components/staff/StaffRecordSummary";
import { StaffStatusGroup } from "@/components/staff/StaffStatusGroup";
import { StaffWorkspaceFooter } from "@/components/staff/StaffWorkspaceFooter";
import { StaffWorkspaceTabs } from "@/components/staff/StaffWorkspaceTabs";
import { Avatar } from "@/components/ui/Avatar";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { useAddressSection } from "@/hooks/use-address-section";
import type { useAccountCommand } from "@/hooks/use-account-command";
import { privateApiFetch } from "@/lib/api-browser";
import type { CustomerSupportAction } from "@/lib/accounts/customer-support";
import {
  customerStatus,
  parseCustomerAccountDetail,
  type CustomerAccountDetail,
  type CustomerAccountRow,
} from "@/lib/accounts/customer-accounts";
import { AccountEditDialog } from "./AccountEditDialog";
import { AccountHistory } from "./AccountHistory";
import { CustomerSupportDialog } from "./CustomerSupportDialog";
import { AccountSuspensionDialog } from "./AccountSuspensionDialog";
import { StaffAccountStatus } from "./StaffAccountStatus";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import shared from "@/components/staff/StaffDetails.module.css";
import styles from "./StaffAccounts.module.css";

type Section = "details" | "history";

// One customer, laid out like the other drawers. The full contact details are read when the drawer opens and
// kept for the session; the NRIC arrives masked.
export function CustomerAccountDrawer({
  row,
  command,
  onClose,
  onChanged,
}: {
  row: CustomerAccountRow | null;
  command: ReturnType<typeof useAccountCommand>;
  onClose: () => void;
  onChanged: () => void;
}) {
  const [section, chooseSection] = useAddressSection<Section>(["details", "history"]);
  const [historyOpened, setHistoryOpened] = useState(section === "history");
  const [suspension, setSuspension] = useState<"suspend" | "lift" | null>(null);
  const [support, setSupport] = useState<CustomerSupportAction | null>(null);
  const [editing, setEditing] = useState(false);
  // Bumped after an edit so the full details are read again.
  const [edits, setEdits] = useState(0);
  // A revealed number is shown only until the drawer changes customer or closes.
  const [revealed, setRevealed] = useState<{ id: string; nric: string } | null>(null);
  const [read, setRead] = useState<{ id: string; detail: CustomerAccountDetail | null } | null>(
    null,
  );
  const id = row?.id ?? "";

  useEffect(() => {
    if (!id) {
      return;
    }
    const attempt = new AbortController();
    privateApiFetch(`/api/customer-accounts/${id}`, parseCustomerAccountDetail, {
      signal: attempt.signal,
    })
      .then((result) => {
        if (!attempt.signal.aborted) {
          setRead({ id, detail: result.status === 200 ? result.data : null });
        }
      })
      .catch(() => {
        if (!attempt.signal.aborted) {
          setRead({ id, detail: null });
        }
      });
    return () => attempt.abort();
  }, [id, edits, row?.suspended, row?.verified]);

  const detail = read?.id === id ? read.detail : null;
  const suspended = row?.suspended === true;

  async function run(action: Promise<boolean>) {
    const done = await action;
    if (done) {
      onChanged();
    }
    return done;
  }

  return (
    <Dialog
      open={Boolean(row)}
      variant="drawer"
      density="compact"
      title={row?.name ?? "Customer"}
      onClose={onClose}
      closeDisabled={command.busy}
    >
      {row && (
        <div className={workbench.workspace}>
          <StaffRecordSummary
            ariaLabel="Customer and status"
            label="Customer"
            value={row.name}
            lead={<Avatar accountId={row.id} name={row.name} pictureId={null} size="lg" />}
            status={
              <StaffStatusGroup label="Account status">
                <StaffAccountStatus status={customerStatus(row)} />
              </StaffStatusGroup>
            }
          />
          <StaffWorkspaceTabs
            label="Customer sections"
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
          <div className={shared.panel} hidden={section !== "details"}>
            <div className={`${workbench.body} ${shared.content}`}>
              <section className={shared.section} aria-label="Contact">
                <div className={workbench.sectionHeader}>
                  <h2 className={workbench.sectionTitle}>Contact</h2>
                  {detail && (
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
                {detail ? (
                  <dl className={shared.details}>
                    <dt>Email</dt>
                    <dd>{detail.email}</dd>
                    <dt>Mobile</dt>
                    <dd>{detail.mobile}</dd>
                    <dt>Address</dt>
                    <dd>{detail.address}</dd>
                    <dt>Username</dt>
                    <dd>@{detail.username}</dd>
                    <dt>NRIC</dt>
                    <dd>{revealed?.id === id ? revealed.nric : detail.nric}</dd>
                  </dl>
                ) : (
                  <p className={styles.note}>
                    {read?.id === id ? "The account could not be read." : "Loading the account…"}
                  </p>
                )}
              </section>
              <section className={shared.section} aria-label="Support">
                <div className={workbench.sectionHeader}>
                  <h2 className={workbench.sectionTitle}>Support</h2>
                </div>
                <ul className={styles.supportList}>
                  <li>
                    <span>
                      <strong>Reveal NRIC</strong>
                      <span>Shows the full number until you close the account.</span>
                    </span>
                    <Button
                      variant="secondary"
                      size="compact"
                      disabled={command.busy}
                      onClick={() => setSupport("nric-reveal")}
                    >
                      Reveal
                    </Button>
                  </li>
                  {!row.verified && (
                    <li>
                      <span>
                        <strong>Email verification</strong>
                        <span>Sends the verification link again.</span>
                      </span>
                      <Button
                        variant="secondary"
                        size="compact"
                        disabled={command.busy}
                        onClick={() => setSupport("verification-resend")}
                      >
                        Resend
                      </Button>
                    </li>
                  )}
                  <li>
                    <span>
                      <strong>Password</strong>
                      <span>Sends the usual reset link.</span>
                    </span>
                    <Button
                      variant="secondary"
                      size="compact"
                      disabled={command.busy || suspended}
                      onClick={() => setSupport("password-reset")}
                    >
                      Send reset
                    </Button>
                  </li>
                  <li>
                    <span>
                      <strong>Sessions</strong>
                      <span>Signs out every device.</span>
                    </span>
                    <Button
                      variant="secondary"
                      size="compact"
                      disabled={command.busy}
                      onClick={() => setSupport("session-revocation")}
                    >
                      End all
                    </Button>
                  </li>
                </ul>
                <p className={styles.note}>
                  Each action asks for a reason and is recorded in the account history.
                </p>
              </section>
            </div>
          </div>
          {historyOpened && (
            <div className={shared.panel} hidden={section !== "history"}>
              <div className={`${workbench.body} ${shared.content}`}>
                <AccountHistory key={row.id} accountId={row.id} />
              </div>
            </div>
          )}
          <StaffWorkspaceFooter
            title="Access"
            description={
              suspended
                ? "Lifting lets them sign in again."
                : "Blocks sign-in, bookings and sales go on."
            }
          >
            <Button
              variant={suspended ? "primary" : "danger"}
              size="compact"
              disabled={command.busy}
              onClick={() => setSuspension(suspended ? "lift" : "suspend")}
            >
              {suspended ? "Lift suspension" : "Suspend account"}
            </Button>
          </StaffWorkspaceFooter>
        </div>
      )}
      <AccountEditDialog
        open={editing}
        subject={
          detail
            ? {
                name: detail.name,
                mobile: detail.mobile,
                address: detail.address,
                email: detail.email,
              }
            : null
        }
        command={command}
        onSubmit={async (draft) => {
          const done = row ? await run(command.edit(row.id, draft)) : false;
          if (done) {
            setEdits((count) => count + 1);
          }
          return done;
        }}
        onClose={() => setEditing(false)}
      />
      <CustomerSupportDialog
        open={support !== null}
        action={support}
        name={row?.name ?? ""}
        command={command}
        onSubmit={async (draft) => {
          if (!row || !support) {
            return false;
          }
          if (support === "nric-reveal") {
            const nric = await command.reveal(row.id, draft);
            setRevealed(nric ? { id: row.id, nric } : null);
            return nric !== null;
          }
          return run(command.support(row.id, support, draft));
        }}
        onClose={() => setSupport(null)}
      />
      <AccountSuspensionDialog
        open={suspension !== null}
        lifting={suspension === "lift"}
        name={row?.name ?? ""}
        command={command}
        customer
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
