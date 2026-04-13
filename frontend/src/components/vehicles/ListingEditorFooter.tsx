import { Button } from "@/components/ui/Button";
import { StaffWorkspaceFooter } from "@/components/staff/StaffWorkspaceFooter";
import type { useListingEditor } from "@/hooks/use-listing-editor";
import type { ListingSection } from "@/lib/vehicles/listings";
import { MAX_LISTING_PHOTOS } from "@/lib/vehicles/listing-photos";

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
            <Button
              size="compact"
              variant="secondary"
              disabled={editor.operating}
              onClick={editor.discard}
            >
              Discard changes
            </Button>
          )}
        </>
      )}
      {(publication || editor.approval.busy || editor.approval.uncertain) && (
        <Button
          size="compact"
          disabled={editor.approval.busy || (!editor.approval.uncertain && !editor.canApprove)}
          onClick={() => void editor.approve()}
        >
          {editor.approval.uncertain ? "Reconcile publication approval" : "Approve publication"}
        </Button>
      )}
      {(publication || editor.withdrawal.busy || editor.withdrawal.uncertain) && (
        <Button
          size="compact"
          variant="danger"
          disabled={editor.withdrawal.busy || (!editor.withdrawal.uncertain && !editor.canWithdraw)}
          onClick={() => void editor.withdraw()}
        >
          {editor.withdrawal.uncertain ? "Reconcile listing withdrawal" : "Withdraw listing"}
        </Button>
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
      description: "Reconcile the captured action before continuing.",
    };
  }
  if (historical) {
    return {
      title: section === "publication" ? "Historical publication" : "Historical listing",
      description: section === "publication"
        ? "Approval and withdrawal records are read only. Your current draft is retained."
        : "Saved fields are read only; identity and mileage reflect the current vehicle record.",
    };
  }
  if (editor.value.photoIds.length > MAX_LISTING_PHOTOS) {
    return {
      title: "Photo limit exceeded",
      description: "Keep up to four photos in Listing, then save the draft before approval.",
    };
  }
  if (editor.upload.file) {
    return {
      title: editor.upload.uncertain ? "Photo upload unconfirmed" : "Photo selected",
      description: "Open Listing to upload the selected photo or check its receipt.",
    };
  }
  if (editor.stale) {
    return {
      title: "Newer saved draft available",
      description: "Discard local changes in Listing to use the newer saved draft.",
    };
  }
  if (editor.dirty) {
    return {
      title: "Unsaved draft changes",
      description: "Your edits are retained. Save or discard them in Listing before approval.",
    };
  }
  if (section === "history") {
    return {
      title: "Listing history",
      description: "Saved revisions and publication records are read only.",
    };
  }
  if (section === "publication") {
    return {
      title: "Publication review",
      description: "Approve the saved draft separately. Withdrawal takes effect immediately.",
    };
  }
  return {
    title: "Listing draft",
    description: "Save changes before approval.",
  };
}
