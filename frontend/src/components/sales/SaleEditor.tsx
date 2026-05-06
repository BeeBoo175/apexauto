"use client";

import { useEffect, useState, type ComponentProps, type ReactNode } from "react";
import { createPortal } from "react-dom";
import { StaffWorkspaceFooter } from "@/components/staff/StaffWorkspaceFooter";
import { StaffWorkspaceTabs } from "@/components/staff/StaffWorkspaceTabs";
import { Button } from "@/components/ui/Button";
import { ConfirmationButton } from "@/components/ui/ConfirmationButton";
import { Textarea } from "@/components/ui/Input";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { useSaleEditor } from "@/hooks/use-sale-editor";
import type { useSaleSelection } from "@/hooks/use-sale-selection";
import { formatCurrency } from "@/lib/currency-format";
import type { Sale } from "@/lib/sales/sale";
import { saleSectionLabels, saleSections, validSaleReason } from "@/lib/sales/sale-sections";
import { SaleAssignDialog } from "./SaleAssignDialog";
import { SaleFacts } from "./SaleFacts";
import { SaleLines } from "./SaleLines";
import { SalePastVersion } from "./SalePastVersion";
import { SaleRecord } from "./SaleRecord";
import { SaleTermsForm } from "./SaleTermsForm";
import { SaleTimeline } from "./SaleTimeline";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import forms from "@/components/settings/SettingsForms.module.css";

const tabs = saleSections.map((value) => ({
  value,
  label: saleSectionLabels[value],
  available: true,
}));

// Inactive sections stay mounted, hidden, so terms and the decision note survive a tab change.
function SalePanel({ active, children }: { active: boolean; children: ReactNode }) {
  return (
    <div hidden={!active}>
      <div className={forms.stackTop}>{children}</div>
    </div>
  );
}

function footerNote(sale: Sale, dirty: boolean) {
  if (dirty) {
    return "Save your term changes before submitting.";
  }
  if (sale.actions.decide) {
    return "Approve the submitted terms, or return them to the agent with a reason.";
  }
  const notes: Record<Sale["sale"]["status"], string> = {
    Open: "Reserved online and waiting for an agent.",
    Handling: "Key the terms agreed with the customer, then submit.",
    PendingApproval: "Waiting for a manager to approve the discount.",
    Approved: "Approved. Settlement and handover follow.",
    Expired: "This hold expired and its deposit was refunded.",
    Cancelled: "This sale is cancelled and its deposit refunded.",
  };
  return notes[sale.sale.status];
}

// The sale's record, terms, history and actions, hosted by the agent's Sales pane and the managers' drawer alike.
export function SaleEditor({
  sale,
  selection,
  now,
  actionsContainer = null,
  refreshControl,
  scrollWithHost = false,
  onChanged,
  onLocked,
}: {
  sale: Sale;
  selection: ReturnType<typeof useSaleSelection>;
  now: number;
  actionsContainer?: HTMLElement | null;
  refreshControl?: ComponentProps<typeof RefreshButton>;
  scrollWithHost?: boolean;
  // Tells the host to refresh what lists this sale; the sale itself is read again here.
  onChanged: () => void;
  onLocked?: (locked: boolean) => void;
}) {
  const editor = useSaleEditor(sale, onChanged);
  const { snapshot } = editor;
  const actions = snapshot.actions;
  const [reason, setReason] = useState("");
  const [assigning, setAssigning] = useState(false);
  // History is read only once its section has been opened.
  const [historyOpened, setHistoryOpened] = useState(selection.section === "history");
  const section = selection.section;
  const locked = editor.dirty || editor.operating;
  const reasonOk = validSaleReason(reason);
  const noteNeeded = actions.decide || actions.cancel;
  const disabled = editor.operating || editor.refreshing;
  // An unconfirmed command keeps only its own control, which replays the same operation without asking again.
  const blocked = (name: Parameters<typeof editor.run>[0]) =>
    editor.busy ||
    editor.refreshing ||
    (editor.uncertain !== undefined && editor.uncertain !== name);
  const replay = (name: Parameters<typeof editor.run>[0]) => editor.uncertain === name;

  useEffect(() => {
    onLocked?.(locked);
    return () => onLocked?.(false);
  }, [locked, onLocked]);

  async function act(name: Parameters<typeof editor.run>[0], withReason = false) {
    if (await editor.run(name, withReason ? { reason } : {})) {
      setReason("");
    }
  }

  const refresh = {
    label: refreshControl?.label ?? "Refresh sale",
    busy: editor.refreshing || Boolean(refreshControl?.busy),
    disabled: editor.operating || Boolean(refreshControl?.disabled),
    onClick: () => {
      void editor.refresh();
      refreshControl?.onClick();
    },
  };

  return (
    <>
      <LoadingProgress label="Updating sale" active={editor.busy || editor.refreshing} />
      {actionsContainer && createPortal(<RefreshButton {...refresh} />, actionsContainer)}
      <div className={workbench.workspace} data-scroll-with-host={scrollWithHost || undefined}>
        <SaleRecord editor={editor} />
        <StaffWorkspaceTabs
          label="Sale sections"
          tabs={tabs}
          value={section}
          disabled={editor.busy}
          onChange={(next) => {
            selection.changeSection(next);
            setHistoryOpened((opened) => opened || next === "history");
          }}
        />
        <div className={workbench.body}>
          {selection.version !== null && section !== "history" && (
            <SalePastVersion
              key={`${selection.version}:${section}`}
              saleId={snapshot.id}
              version={selection.version}
              section={section}
              context={snapshot.context}
              now={now}
              onBack={() => selection.openVersion(null)}
            />
          )}
          <div hidden={selection.version !== null && section !== "history"}>
            <SalePanel active={section === "customer"}>
              <SaleFacts
                sale={snapshot.sale}
                context={snapshot.context}
                agent={snapshot.agent}
                decidedBy={snapshot.decidedBy}
                now={now}
              />
            </SalePanel>
            <SalePanel active={section === "deal"}>
              <SaleLines sale={snapshot.sale} />
              {snapshot.sale.discountLimitAtSubmit !== null && (
                <p className={workbench.help}>
                  Agent discount limit when submitted:{" "}
                  {formatCurrency(snapshot.sale.discountLimitAtSubmit)}.
                </p>
              )}
              {editor.stale && (
                <div className={workbench.protocol} role="status">
                  <p>A newer version was loaded. Your entries are kept until you use it.</p>
                  <Button size="compact" variant="secondary" onClick={editor.useLatest}>
                    Use latest values
                  </Button>
                </div>
              )}
              {actions.saveTerms && (
                <SaleTermsForm
                  key={`${editor.base.id}:${editor.base.sale.version}`}
                  editor={editor}
                  disabled={blocked("terms")}
                />
              )}
              {noteNeeded && (
                <section className={workbench.stack}>
                  <div className={workbench.sectionHeader}>
                    <h2 className={workbench.sectionTitle}>Decision note</h2>
                  </div>
                  <Textarea
                    label="Reason"
                    labelHidden
                    placeholder="Required to return or cancel this sale"
                    maxLength={1000}
                    rows={3}
                    value={reason}
                    disabled={disabled}
                    onChange={(event) => setReason(event.target.value.replace(/^\s+/, ""))}
                  />
                </section>
              )}
            </SalePanel>
          </div>
          {historyOpened && (
            <div hidden={section !== "history"}>
              <SaleTimeline
                key={snapshot.sale.version}
                saleId={snapshot.id}
                disabled={editor.busy}
                onOpenVersion={(version, versionSection) =>
                  selection.openVersion(version, versionSection)
                }
              />
            </div>
          )}
        </div>
        <StaffWorkspaceFooter title="Sale actions" description={footerNote(snapshot, editor.dirty)}>
          {actions.claim && (
            <ConfirmationButton
              confirmationTitle="Claim this sale?"
              message="You become its agent and the hold's timer pauses until you release it."
              size="compact"
              disabled={blocked("claims")}
              skipConfirmation={replay("claims")}
              onConfirm={() => act("claims")}
            >
              Claim
            </ConfirmationButton>
          )}
          {actions.submit && (
            <ConfirmationButton
              confirmationTitle="Submit this sale?"
              message="Submitting captures today's fee, tax and discount limit. A discount within your limit is approved at once; a larger one goes to a manager."
              size="compact"
              disabled={blocked("submissions") || editor.dirty}
              skipConfirmation={replay("submissions")}
              onConfirm={() => act("submissions")}
            >
              Submit
            </ConfirmationButton>
          )}
          {actions.decide && (
            <>
              <ConfirmationButton
                confirmationTitle="Approve this sale?"
                message="The sale becomes approved with its submitted terms."
                size="compact"
                disabled={blocked("approvals")}
                skipConfirmation={replay("approvals")}
                onConfirm={() => act("approvals")}
              >
                Approve
              </ConfirmationButton>
              <ConfirmationButton
                confirmationTitle="Return this sale to the agent?"
                message="The agent keeps the terms and can revise and submit again."
                size="compact"
                variant="secondary"
                disabled={blocked("rejections") || !reasonOk}
                skipConfirmation={replay("rejections")}
                onConfirm={() => act("rejections", true)}
              >
                Return to agent
              </ConfirmationButton>
            </>
          )}
          {actions.assign && (
            <Button
              size="compact"
              variant="secondary"
              disabled={blocked("assignments")}
              onClick={() => setAssigning(true)}
            >
              {snapshot.sale.agentId ? "Reassign" : "Assign"}
            </Button>
          )}
          {actions.release && (
            <ConfirmationButton
              confirmationTitle="Release this sale?"
              message="The hold returns to the queue with the time it had left, and its terms go back to the reservation's. Earlier terms stay in the history."
              size="compact"
              variant="secondary"
              disabled={blocked("releases")}
              skipConfirmation={replay("releases")}
              onConfirm={() => act("releases")}
            >
              Release
            </ConfirmationButton>
          )}
          {actions.cancel && (
            <ConfirmationButton
              confirmationTitle="Cancel this sale?"
              message="This is final. The deposit is refunded and the car becomes available again."
              size="compact"
              variant="danger"
              disabled={blocked("cancellations") || !reasonOk}
              skipConfirmation={replay("cancellations")}
              onConfirm={() => act("cancellations", true)}
            >
              Cancel sale
            </ConfirmationButton>
          )}
        </StaffWorkspaceFooter>
      </div>
      {actions.assign && (
        <SaleAssignDialog editor={editor} open={assigning} onClose={() => setAssigning(false)} />
      )}
    </>
  );
}
