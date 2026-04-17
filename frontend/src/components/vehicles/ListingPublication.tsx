import type { useListingEditor } from "@/hooks/use-listing-editor";
import { Textarea } from "@/components/ui/Input";
import { listingWithdrawalReason } from "@/lib/vehicles/listing-withdrawal";
import type { ListingWorkspace } from "@/lib/vehicles/listing-workspace";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import styles from "./ListingPublication.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";

export function ListingPublication({
  snapshot,
  editor,
}: {
  snapshot: ListingWorkspace;
  editor: ReturnType<typeof useListingEditor>;
}) {
  const showWithdrawal = Boolean(
    editor.hasActivePublication ||
    editor.withdrawal.busy ||
    editor.withdrawal.uncertain ||
    editor.withdrawalTargetChanged ||
    editor.withdrawalReason,
  );
  const hasUnpublishedChanges = Boolean(
    editor.base &&
    editor.base.id !== snapshot.publication?.revisionId &&
    !editor.approval.awaitingRefresh,
  );

  return (
    <div className={styles.content}>
      <section className={styles.section} aria-label="Saved draft for publication">
        <div className={workbench.sectionHeader}>
          <h2 className={workbench.sectionTitle}>Saved listing</h2>
          {editor.base && <span className={styles.metadata}>Version {editor.base.version}</span>}
        </div>
        {editor.base && (
          <dl className={styles.facts}>
            <div className={styles.price}>
              <dt>Vehicle price</dt>
              <dd>{formatCurrency(editor.base.vehiclePrice)}</dd>
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
        )}
        {hasUnpublishedChanges && (
          <p className={styles.help}>
            {editor.hasActivePublication
              ? "These changes are not public yet. Approve them to update the listing."
              : "Approve these saved details to put the listing online."}
          </p>
        )}
        {!editor.base && (
          <p className={styles.help}>
            Prepare and save the listing details before approving publication.
          </p>
        )}
      </section>
      {editor.approval.receipt && editor.approval.receipt.id !== snapshot.publication?.id && (
        <section
          className={`${styles.section} ${styles.receipt}`}
          aria-label="Confirmed approval receipt"
        >
          <h3>Publication confirmed</h3>
          <p className={styles.help}>
            Approved {formatMalaysianDateTime(editor.approval.receipt.recordedAt)}. Refresh to
            update the displayed publication status.
          </p>
        </section>
      )}
      {editor.withdrawal.receipt &&
        editor.withdrawal.receipt.publicationId !== snapshot.withdrawal?.publicationId && (
          <section
            className={`${styles.section} ${styles.receipt}`}
            aria-label="Confirmed withdrawal receipt"
          >
            <h3>Withdrawal confirmed</h3>
            <p className={styles.help}>
              Withdrawn {formatMalaysianDateTime(editor.withdrawal.receipt.recordedAt)}.
            </p>
            <div className={styles.recordText}>
              <h4>Reason</h4>
              <p>{editor.withdrawal.receipt.reason}</p>
            </div>
          </section>
        )}
      {showWithdrawal && (
        <section
          className={`${styles.section} ${styles.withdrawalForm}`}
          aria-label="Withdraw listing"
        >
          <h3>Withdraw approved listing</h3>
          <p className={styles.help}>
            Withdrawal takes effect immediately. Any active unconverted hold is cancelled with a
            full simulated refund. Unsaved listing edits remain in this drawer.
          </p>
          <Textarea
            label="Withdrawal reason"
            required
            maxLength={1000}
            rows={3}
            value={editor.withdrawalReason}
            disabled={editor.operating}
            aria-invalid={
              editor.withdrawalValidationAttempted &&
              !listingWithdrawalReason(editor.withdrawalReason)
            }
            onChange={(event) => editor.changeWithdrawalReason(event.target.value)}
          />
          {editor.withdrawalTargetChanged && (
            <p className={styles.help}>
              The publication changed. Clear this reason before withdrawing the current publication.
            </p>
          )}
          {!editor.withdrawalTargetChanged &&
            !editor.hasActivePublication &&
            editor.withdrawalReason && (
              <p className={styles.help}>
                This publication is no longer active. Clear the reason to continue.
              </p>
            )}
        </section>
      )}
    </div>
  );
}
