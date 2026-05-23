"use client";

import { StaffWorkspaceFooter } from "@/components/staff/StaffWorkspaceFooter";
import { Button } from "@/components/ui/Button";
import { ConfirmationButton } from "@/components/ui/ConfirmationButton";
import type { useSaleEditor } from "@/hooks/use-sale-editor";
import type { useSaleSelection } from "@/hooks/use-sale-selection";
import { normalizeSaleReason } from "@/lib/sales/parse";
import { saleFooterPresentation } from "@/lib/sales/sale-footer";
import { isSaleVersionSection, saleSectionLabels } from "@/lib/sales/sale-sections";
import workspace from "@/components/staff/StaffWorkspace.module.css";

export function SaleFooter({
  editor,
  selection,
  onAssign,
  onActionsContainer,
  completionActive = false,
  completionLoading = false,
  completionStage = null,
  correctingDocument = false,
  onPrepareCompletion,
  externalOperating = false,
}: {
  editor: ReturnType<typeof useSaleEditor>;
  selection: ReturnType<typeof useSaleSelection>;
  onAssign: () => void;
  onActionsContainer: (container: HTMLDivElement | null) => void;
  completionActive?: boolean;
  completionLoading?: boolean;
  completionStage?: "settlement" | "handover" | null;
  correctingDocument?: boolean;
  onPrepareCompletion?: () => void;
  externalOperating?: boolean;
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
  const delivery = selection.section === "delivery";
  const handoverAssignment = snapshot.sale.status === "Settled";
  const showsAssignment = handoverAssignment ? delivery : deal;
  const { title, description } = saleFooterPresentation({
    status: snapshot.sale.status,
    actions,
    section: selection.section,
    version: selection.version,
    editing,
    dirty: editor.dirty,
    notesDirty: editor.notesDirty,
    cancelling,
  });
  const blocked = editor.commandBlocked;
  const replay = (name: Parameters<typeof editor.run>[0]) => editor.uncertain === name;

  if (completionActive || completionLoading) {
    let taskTitle = "Review settlement";
    let taskDescription = "Confirm buyer details and payment before issuing documents.";
    if (completionStage === "handover") {
      taskTitle = "Complete handover";
      taskDescription = "Record the odometer, keys and delivery confirmations.";
    }
    if (correctingDocument) {
      taskTitle = "Correct buyer details";
      taskDescription = "The original document remains available after reissue.";
    }
    return (
      <StaffWorkspaceFooter
        title={completionLoading ? "Loading buyer details" : taskTitle}
        description={
          delivery ? taskDescription : "Your entries are kept. Return to Delivery to continue."
        }
      >
        <div ref={onActionsContainer} className={workspace.footerActions} />
        {!delivery && (
          <Button
            size="compact"
            disabled={editor.operating || completionLoading || externalOperating}
            onClick={() => selection.changeSection("delivery")}
          >
            Return to editing
          </Button>
        )}
      </StaffWorkspaceFooter>
    );
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
          {showsAssignment &&
            actions.assign &&
            snapshot.sale.status !== "Open" &&
            !editor.notesDirty && (
              <Button
                size="compact"
                variant="secondary"
                disabled={blocked("assignments") || editor.notesDirty}
                onClick={onAssign}
              >
                {handoverAssignment ? "Change handover agent" : "Reassign"}
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
              onConfirm={() => editor.run("claims", {})}
            >
              {replay("claims") ? "Reconcile claim" : "Claim"}
            </ConfirmationButton>
          )}
          {!deal && !actions.claim && snapshot.sale.status !== "Open" && !editor.notesDirty && (
            <Button
              size="compact"
              variant="secondary"
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
              onConfirm={() => editor.run("submissions", {})}
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
                  onConfirm={() => editor.run("approvals", {})}
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
          {(deal || delivery) && onPrepareCompletion && !editor.notesDirty && (
            <Button
              size="compact"
              variant={delivery ? "primary" : "secondary"}
              disabled={editor.locked || editor.refreshing || editor.confirmed}
              onClick={delivery ? onPrepareCompletion : () => selection.changeSection("delivery")}
            >
              {!delivery && "Open delivery"}
              {delivery &&
                (snapshot.sale.status === "Approved" ? "Review settlement" : "Prepare handover")}
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
          variant="secondary"
          disabled={editor.busy || editor.refreshing}
          onClick={() => selection.openVersion(null)}
        >
          Back to current {pastLabel}
        </Button>
      )}
    </StaffWorkspaceFooter>
  );
}
