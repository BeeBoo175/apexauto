"use client";

import { StaffWorkspaceFooter } from "@/components/staff/StaffWorkspaceFooter";
import { Button } from "@/components/ui/Button";
import { ConfirmationButton } from "@/components/ui/ConfirmationButton";
import type { useSaleEditor } from "@/hooks/use-sale-editor";
import type { useSaleSelection } from "@/hooks/use-sale-selection";
import { normalizeSaleReason } from "@/lib/sales/parse";
import type { Sale } from "@/lib/sales/contracts";
import { isSaleVersionSection, saleSectionLabels } from "@/lib/sales/sale-sections";
import workspace from "@/components/staff/StaffWorkspace.module.css";

function footerNote(sale: Sale, dirty: boolean) {
  if (dirty) {
    return "Save your term changes before submitting.";
  }
  if (sale.actions.decide) {
    return "Approve the submitted terms, or return them to the agent with a reason.";
  }
  if (sale.sale.status === "Handling" && !sale.actions.saveTerms) {
    return "Review the agreed terms or manage this sale’s assignment.";
  }
  const notes: Record<Sale["sale"]["status"], string> = {
    Open: sale.actions.claim
      ? "Claim this sale to start agreeing terms."
      : "Assign an agent to start agreeing terms.",
    Handling: "Key the terms agreed with the customer, then submit.",
    PendingApproval: "Waiting for a manager to approve the discount.",
    Approved: "Settlement and handover follow.",
    Expired: "This hold expired and its deposit was refunded.",
    Cancelled: "This sale is cancelled and its deposit refunded.",
  };
  return notes[sale.sale.status];
}

export function SaleFooter({
  editor,
  selection,
  onAssign,
  onActionsContainer,
}: {
  editor: ReturnType<typeof useSaleEditor>;
  selection: ReturnType<typeof useSaleSelection>;
  onAssign: () => void;
  onActionsContainer: (container: HTMLDivElement | null) => void;
}) {
  const { snapshot, reason } = editor;
  const actions = snapshot.actions;
  const cancelling = actions.cancel && Boolean(editor.cancellationReason.trim());
  const returning = actions.decide && Boolean(reason.trim()) && !cancelling;
  const viewingPast = selection.version !== null && isSaleVersionSection(selection.section);
  const pastLabel = saleSectionLabels[selection.section].toLowerCase();
  const reasonOk = normalizeSaleReason(reason) !== null;
  const editing = editor.editingTerms;
  const editingHere = selection.section === "deal" && !viewingPast && editing;
  const deal = selection.section === "deal";
  let title = "Next step";
  if (editing) {
    title = editor.dirty ? "Unsaved deal terms" : "Edit deal terms";
  }
  if (viewingPast) {
    title = `Historical ${pastLabel} · version ${selection.version}`;
  }
  if (selection.section === "history") {
    title = "Record history";
  }
  let description = footerNote(snapshot, editor.dirty);
  if (!deal && selection.section !== "history" && snapshot.sale.status !== "Open") {
    description = "Review the current deal and agreed terms.";
  }
  if (editing) {
    description = "Save the agreed terms, or discard changes to restore the confirmed deal.";
  }
  if (viewingPast || selection.section === "history") {
    description = "Retained sale versions are read only.";
  }
  if (editing && !editingHere) {
    title = editor.dirty ? "Unsaved deal terms" : "Edit deal terms";
    description = "Your terms are kept. Return to Deal to save or discard changes.";
  }
  if (editor.notesDirty) {
    title = cancelling ? "Cancel sale" : "Return to agent";
    description = "Complete this action, or discard changes to continue reviewing the sale.";
    if (!deal || viewingPast) {
      description =
        "Your reason is kept. Return to Deal to complete the action, or discard changes.";
    }
  }
  const blocked = (name: Parameters<typeof editor.run>[0]) =>
    editor.busy ||
    editor.refreshing ||
    editor.confirmed ||
    (editor.uncertain !== undefined && editor.uncertain !== name);
  const replay = (name: Parameters<typeof editor.run>[0]) => editor.uncertain === name;

  async function act(name: "claims" | "submissions" | "approvals") {
    await editor.run(name, {});
  }

  return (
    <StaffWorkspaceFooter title={title} description={description}>
      <div ref={onActionsContainer} className={workspace.footerActions} />
      {editor.notesDirty && (
        <ConfirmationButton
          size="compact"
          variant="secondary"
          disabled={editor.operating}
          confirmationTitle="Discard these notes?"
          message="Your unsent return and cancellation reasons will be cleared."
          onConfirm={editor.discardNotes}
        >
          Discard changes
        </ConfirmationButton>
      )}
      {editor.notesDirty && (!deal || viewingPast) && (
        <Button
          size="compact"
          disabled={editor.operating}
          onClick={() => selection.openVersion(null, "deal")}
        >
          Return to editing
        </Button>
      )}
      {editing && !editingHere && (
        <Button
          size="compact"
          variant="secondary"
          disabled={editor.operating}
          onClick={() => selection.openVersion(null, "deal")}
        >
          Return to editing
        </Button>
      )}
      {!viewingPast && !editing && selection.section !== "history" && (
        <>
          {deal && actions.assign && snapshot.sale.status !== "Open" && !editor.notesDirty && (
            <Button
              size="compact"
              variant="secondary"
              disabled={blocked("assignments") || editor.notesDirty}
              onClick={onAssign}
            >
              Reassign
            </Button>
          )}
          {deal && actions.release && !editor.notesDirty && (
            <ConfirmationButton
              size="compact"
              variant="secondary"
              disabled={blocked("releases") || editor.notesDirty}
              confirmationTitle="Release this sale?"
              message="The hold returns to the queue with its remaining time. Terms reset to the reservation; earlier terms stay in History."
              skipConfirmation={replay("releases")}
              onConfirm={() => editor.run("releases", {})}
            >
              {replay("releases") ? "Reconcile release" : "Release"}
            </ConfirmationButton>
          )}
          {cancelling && deal && (
            <ConfirmationButton
              size="compact"
              variant="danger"
              disabled={blocked("cancellations") || !normalizeSaleReason(editor.cancellationReason)}
              confirmationTitle="Cancel this sale?"
              message="This is final. The deposit is refunded and the car becomes available again."
              skipConfirmation={replay("cancellations")}
              onConfirm={() => editor.run("cancellations", { reason: editor.cancellationReason })}
            >
              {replay("cancellations") ? "Reconcile cancellation" : "Cancel sale"}
            </ConfirmationButton>
          )}
          {actions.claim && !cancelling && (
            <ConfirmationButton
              confirmationTitle="Claim this sale?"
              message="You become its agent and the hold's timer pauses until you release it."
              size="compact"
              disabled={blocked("claims")}
              skipConfirmation={replay("claims")}
              onConfirm={() => act("claims")}
            >
              {replay("claims") ? "Reconcile claim" : "Claim"}
            </ConfirmationButton>
          )}
          {!deal && !actions.claim && snapshot.sale.status !== "Open" && !editor.notesDirty && (
            <Button
              size="compact"
              onClick={() => selection.openVersion(null, "deal")}
              disabled={editor.operating}
            >
              Review deal
            </Button>
          )}
          {actions.submit && deal && !cancelling && (
            <ConfirmationButton
              confirmationTitle="Submit this sale?"
              message="Submitting captures today's fee, tax and discount limit. A discount within your limit is approved at once; a larger one goes to a manager."
              size="compact"
              disabled={blocked("submissions") || editor.dirty}
              skipConfirmation={replay("submissions")}
              onConfirm={() => act("submissions")}
            >
              {replay("submissions") ? "Reconcile submission" : "Submit deal"}
            </ConfirmationButton>
          )}
          {actions.decide && deal && !cancelling && (
            <>
              <ConfirmationButton
                confirmationTitle="Return this sale to the agent?"
                message="The agent keeps the terms and can revise and submit again."
                size="compact"
                variant={returning ? "primary" : "secondary"}
                disabled={blocked("rejections") || (!replay("rejections") && !reasonOk)}
                skipConfirmation={replay("rejections")}
                onConfirm={() => editor.run("rejections", { reason })}
              >
                {replay("rejections") ? "Reconcile return" : "Return to agent"}
              </ConfirmationButton>
              {!returning && (
                <ConfirmationButton
                  confirmationTitle="Approve this sale?"
                  message="The sale becomes approved with its submitted terms."
                  size="compact"
                  disabled={blocked("approvals")}
                  skipConfirmation={replay("approvals")}
                  onConfirm={() => act("approvals")}
                >
                  {replay("approvals") ? "Reconcile approval" : "Approve deal"}
                </ConfirmationButton>
              )}
            </>
          )}
          {actions.assign && snapshot.sale.status === "Open" && !cancelling && (
            <Button
              size="compact"
              variant="primary"
              disabled={blocked("assignments")}
              onClick={onAssign}
            >
              Assign agent
            </Button>
          )}
        </>
      )}
      {viewingPast && (
        <Button
          size="compact"
          variant="secondary"
          disabled={editor.operating}
          onClick={() => selection.changeSection("history")}
        >
          Back to history
        </Button>
      )}
      {viewingPast && !editing && !editor.notesDirty && (
        <Button
          size="compact"
          variant="primary"
          disabled={editor.busy || editor.refreshing}
          onClick={() => selection.openVersion(null)}
        >
          Back to current {pastLabel}
        </Button>
      )}
    </StaffWorkspaceFooter>
  );
}
