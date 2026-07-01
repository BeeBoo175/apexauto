import { ConfirmationButton } from "@/components/ui/ConfirmationButton";
import { Button } from "@/components/ui/Button";
import { StaffWorkspaceFooter } from "@/components/staff/StaffWorkspaceFooter";
import type { useListingEditor } from "@/hooks/use-listing-editor";
import type { ListingSection } from "@/lib/vehicles/listings";
import { MAX_LISTING_PHOTOS } from "@/lib/vehicles/listing-photos";
import { listingPublicationAction } from "@/lib/vehicles/listing-publication";
import { listingWithdrawalReason } from "@/lib/vehicles/listing-withdrawal";

export function ListingEditorFooter({
  editor,
  section,
  historical,
  onCurrent,
}: {
  editor: ReturnType<typeof useListingEditor>;
  section: ListingSection;
  historical: boolean;
  onCurrent: () => void;
}) {
  const context = footerContext(editor, section, historical);
  const publication = section === "publication" && !historical;
  const draftActions =
    (!historical && (section === "listing" || section === "card")) || editor.command.uncertain;
  const publicationAction = listingPublicationAction({
    current: publication,
    canApprove: editor.canApprove,
    withdrawalReason: editor.withdrawalReason,
    approval: editor.approval,
    withdrawal: editor.withdrawal,
  });
  const republishing = editor.isWithdrawn || editor.approval.republishing;
  let approvalLabel = republishing ? "Republish listing" : "Approve publication";
  if (editor.approval.busy) {
    approvalLabel = republishing ? "Republishing…" : "Approving publication…";
  }
  if (editor.approval.uncertain) {
    approvalLabel = republishing ? "Reconcile republication" : "Reconcile publication approval";
  }
  let withdrawalLabel = "Withdraw listing";
  if (editor.withdrawal.busy) {
    withdrawalLabel = "Withdrawing listing…";
  }
  if (editor.withdrawal.uncertain) {
    withdrawalLabel = "Reconcile listing withdrawal";
  }

  return (
    <StaffWorkspaceFooter title={context.title} description={context.description}>
      {draftActions && (
        <>
          <Button
            size="compact"
            disabled={
              Boolean(editor.upload.file) ||
              editor.approval.busy ||
              editor.approval.uncertain ||
              editor.withdrawal.busy ||
              editor.withdrawal.uncertain ||
              editor.command.busy ||
              (!editor.command.uncertain && (!editor.dirty || editor.stale))
            }
            onClick={() => void editor.save()}
          >
            {editor.command.uncertain ? "Reconcile draft save" : "Save draft"}
          </Button>
          {(editor.dirty || editor.stale) && (
            <ConfirmationButton
              confirmationTitle="Discard listing changes?"
              message="Your unsaved listing edits will be replaced with the latest saved draft."
              size="compact"
              variant="secondary"
              disabled={editor.operating}
              onConfirm={editor.discard}
            >
              Discard changes
            </ConfirmationButton>
          )}
        </>
      )}
      {publicationAction === "approve" && (
        <ConfirmationButton
          confirmationTitle={republishing ? "Republish this listing?" : "Approve publication?"}
          message="The saved listing details and approved price will become publicly available."
          skipConfirmation={editor.approval.uncertain}
          size="compact"
          disabled={editor.approval.busy || (!editor.approval.uncertain && !editor.canApprove)}
          onConfirm={() => editor.approve()}
        >
          {approvalLabel}
        </ConfirmationButton>
      )}
      {publicationAction === "withdraw" && (
        <ConfirmationButton
          confirmationTitle="Withdraw this listing?"
          message="This immediately removes the listing. Any active unconverted hold will be cancelled with a full simulated deposit refund."
          skipConfirmation={editor.withdrawal.uncertain}
          size="compact"
          variant="danger"
          disabled={
            editor.withdrawal.busy ||
            (!editor.withdrawal.uncertain &&
              (!editor.canWithdraw || !listingWithdrawalReason(editor.withdrawalReason)))
          }
          onConfirm={() => editor.withdraw()}
        >
          {withdrawalLabel}
        </ConfirmationButton>
      )}
      {historical && (
        <Button
          size="compact"
          variant={editor.operating ? "secondary" : "primary"}
          onClick={onCurrent}
        >
          {section === "publication" ? "View current publication" : "Back to current listing"}
        </Button>
      )}
    </StaffWorkspaceFooter>
  );
}

function footerContext(
  editor: ReturnType<typeof useListingEditor>,
  section: ListingSection,
  historical: boolean,
) {
  if (editor.command.uncertain || editor.approval.uncertain || editor.withdrawal.uncertain) {
    return {
      title: "Listing action unconfirmed",
      description: "Confirm the earlier action first.",
    };
  }
  if (historical) {
    return {
      title: section === "publication" ? "Historical publication" : "Historical listing",
      description:
        section === "publication"
          ? "Past records are read only; draft kept."
          : "Saved fields are read only.",
    };
  }
  if (editor.withdrawalReason) {
    return {
      title: "Withdraw listing",
      description: editor.withdrawalTargetChanged
        ? "Publication changed. Clear the reason."
        : "Withdrawing needs a reason and confirmation.",
    };
  }
  if (editor.value.photoIds.length > MAX_LISTING_PHOTOS) {
    return {
      title: "Photo limit exceeded",
      description: "Keep four photos at most, then save.",
    };
  }
  if (editor.upload.file) {
    return {
      title: editor.upload.uncertain ? "Photo upload unconfirmed" : "Photo selected",
      description: "Upload the photo or check it in Listing.",
    };
  }
  if (editor.stale) {
    return {
      title: "Newer saved draft available",
      description: "Discard changes to use the newer draft.",
    };
  }
  if (editor.dirty) {
    return {
      title: "Unsaved draft changes",
      description: "Save or discard edits before approval.",
    };
  }
  if (section === "history") {
    return {
      title: "Listing history",
      description: "Past revisions are read only.",
    };
  }
  if (section === "publication") {
    if (editor.approval.awaitingRefresh) {
      return {
        title: "Publication confirmed",
        description: "Refresh to show the publication record.",
      };
    }
    if (editor.isWithdrawn) {
      return {
        title: "Listing withdrawn",
        description: "Republish approves and restores the listing.",
      };
    }
    if (editor.hasActivePublication && !editor.canApprove) {
      return {
        title: "Listing published",
        description: "Withdraw with a reason, or save to update.",
      };
    }
    return {
      title: "Publication review",
      description: "Approve the saved draft to publish it.",
    };
  }
  return {
    title: "Listing draft",
    description: "Save changes before approval.",
  };
}
