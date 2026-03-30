import type { useListingEditor } from "@/hooks/use-listing-editor";
import type { ListingWorkspace } from "@/lib/vehicles/listing-workspace";
import { listingPublication } from "@/lib/vehicles/listings";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import workshop from "@/components/inspection/Workshop.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";

export function ListingPublication({
  snapshot,
  editor,
}: {
  snapshot: ListingWorkspace;
  editor: ReturnType<typeof useListingEditor>;
}) {
  const vehicle = {
    ...snapshot.vehicle,
    revisionId: editor.base?.id ?? null,
    version: editor.base?.version ?? 0,
  };
  return (
    <section className={workbench.stack}>
      <h3>Publication record</h3>
      <p>
        Approve the saved draft as a separate step after saving. Local edits must be saved or
        discarded first. The listing and signed inspection are checked again on approval.
      </p>
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
          <dt>Availability</dt>
          <dd>{listingPublication(vehicle)}</dd>
        </div>
        <div>
          <dt>Current signed inspection</dt>
          <dd>
            {snapshot.currentSignedRevisionId
              ? "Signed inspection recorded"
              : "No current signed inspection"}
          </dd>
        </div>
        <div>
          <dt>Last approved</dt>
          <dd>
            {snapshot.publication
              ? formatMalaysianDateTime(snapshot.publication.recordedAt)
              : "Not approved"}
          </dd>
        </div>
        <div>
          <dt>Approved price</dt>
          <dd>
            {snapshot.approvedRevision
              ? formatCurrency(snapshot.approvedRevision.vehiclePrice)
              : "Not approved"}
          </dd>
        </div>
      </dl>
      {editor.approval.receipt && (
        <dl className={workshop.facts}>
          <div>
            <dt>Confirmed approval receipt</dt>
            <dd>{formatMalaysianDateTime(editor.approval.receipt.recordedAt)}</dd>
          </div>
        </dl>
      )}
      {snapshot.approvedRevision && (
        <>
          <h3>Approved description</h3>
          <p>{snapshot.approvedRevision.description}</p>
        </>
      )}
      {snapshot.withdrawal && (
        <>
          <h3>Withdrawal reason</h3>
          <p>{snapshot.withdrawal.reason}</p>
          <p>{formatMalaysianDateTime(snapshot.withdrawal.recordedAt)}</p>
        </>
      )}
    </section>
  );
}
