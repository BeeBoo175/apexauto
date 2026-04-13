import { Button } from "@/components/ui/Button";
import { StaffWorkspaceFooter } from "@/components/staff/StaffWorkspaceFooter";
import type { useListingEditor } from "@/hooks/use-listing-editor";
import type { ListingSection } from "@/lib/vehicles/listings";

export function ListingEditorFooter({
  editor,
  section,
}: {
  editor: ReturnType<typeof useListingEditor>;
  section: ListingSection;
}) {
  const context = footerContext(editor, section);
  const publication = section === "publication";
  const draftActions =
    (section === "listing" || section === "card") || editor.command.uncertain;

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
    </StaffWorkspaceFooter>
  );
}

function footerContext(
  editor: ReturnType<typeof useListingEditor>,
  section: ListingSection,
) {
  if (editor.command.uncertain || editor.approval.uncertain || editor.withdrawal.uncertain) {
    return {
      title: "Listing action unconfirmed",
      description: "Reconcile the captured action before continuing.",
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