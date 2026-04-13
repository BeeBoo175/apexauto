import type { useListingEditor } from "@/hooks/use-listing-editor";
import { Button } from "@/components/ui/Button";
import { Textarea } from "@/components/ui/Input";
import { listingWithdrawalReason } from "@/lib/vehicles/listing-withdrawal";
import type { ListingWorkspace } from "@/lib/vehicles/listing-workspace";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { ListingPublicationRecord } from "./ListingPublicationRecord";
import workshop from "@/components/inspection/Workshop.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";

export function ListingPublication({
  snapshot,
  editor,
}: {
  snapshot: ListingWorkspace;
  editor: ReturnType<typeof useListingEditor>;
}) {
  const showWithdrawal = Boolean(
    snapshot.publication &&
      (editor.withdrawal.busy ||
        editor.withdrawal.uncertain ||
        editor.withdrawalTargetChanged ||
        editor.withdrawalReason ||
        (!snapshot.withdrawal && editor.withdrawal.receipt?.publicationId !== snapshot.publication.id)),
  );
  return (
    <section className={workbench.stack}>
      <h3>Saved draft</h3>
      <dl className={workshop.facts}>
        <div>
          <dt>Draft to approve</dt>
          <dd>{editor.base ? `Saved version ${editor.base.version}` : "No saved draft"}</dd>
        </div>
        <div>
          <dt>Saved draft price</dt>
          <dd>{editor.base ? formatCurrency(editor.base.vehiclePrice) : "Not prepared"}</dd>
        </div>
        <div>
          <dt>Current signed inspection</dt>
          <dd>
            {snapshot.currentSignedRevisionId
              ? "Signed inspection recorded"
              : "No current signed inspection"}
          </dd>
        </div>
      </dl>
      {editor.approval.receipt && editor.approval.receipt.id !== snapshot.publication?.id && (
        <dl className={workshop.facts}>
          <div>
            <dt>Confirmed approval receipt</dt>
            <dd>{formatMalaysianDateTime(editor.approval.receipt.recordedAt)}</dd>
          </div>
        </dl>
      )}
      <ListingPublicationRecord
        revision={snapshot.approvedRevision}
        publication={snapshot.publication}
        withdrawal={snapshot.withdrawal}
      />
      {editor.withdrawal.receipt &&
        editor.withdrawal.receipt.publicationId !== snapshot.withdrawal?.publicationId && (
        <dl className={workshop.facts}>
          <div>
            <dt>Confirmed withdrawal receipt</dt>
            <dd>{formatMalaysianDateTime(editor.withdrawal.receipt.recordedAt)}</dd>
          </div>
          <div>
            <dt>Confirmed reason</dt>
            <dd>{editor.withdrawal.receipt.reason}</dd>
          </div>
        </dl>
      )}
      {showWithdrawal && (
        <>
          <h3>Withdraw approved listing</h3>
          <p>
            Withdrawal takes effect immediately. Any active unconverted hold is cancelled with a
            full simulated refund. Unsaved listing edits remain in this drawer.
          </p>
          <Textarea
            label="Withdrawal reason"
            required
            maxLength={1000}
            rows={3}
            value={editor.withdrawalReason}
            disabled={editor.operating || (!editor.canWithdraw && !editor.withdrawalTargetChanged)}
            aria-invalid={
              editor.withdrawalValidationAttempted &&
              !listingWithdrawalReason(editor.withdrawalReason)
            }
            onChange={(event) => editor.changeWithdrawalReason(event.target.value)}
          />
          {editor.withdrawalTargetChanged && (
            <p>The approved publication changed. Discard this reason before withdrawing another.</p>
          )}
          {editor.withdrawalReason && (
            <Button
              size="compact"
              variant="secondary"
              disabled={editor.operating}
              onClick={editor.discardWithdrawal}
            >
              Discard withdrawal reason
            </Button>
          )}
        </>
      )}
    </section>
  );
}
