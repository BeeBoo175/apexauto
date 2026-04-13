import { StaffLink } from "@/components/staff/StaffLink";
import type { ListingRevision, ListingWorkspace } from "@/lib/vehicles/listing-workspace";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import workshop from "@/components/inspection/Workshop.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";

export function ListingPublicationRecord({
  revision,
  publication,
  withdrawal,
  signedReport = null,
}: {
  revision: ListingRevision | null;
  publication: ListingWorkspace["publication"];
  withdrawal: ListingWorkspace["withdrawal"];
  signedReport?: ListingWorkspace["currentSignedReport"];
}) {
  if (!publication || !revision) {
    return <p>No publication has been approved.</p>;
  }

  return (
    <section className={workbench.stack} aria-label="Approved publication record">
      <h3>Approved publication</h3>
      <dl className={workshop.facts}>
        <div>
          <dt>Listing revision</dt>
          <dd>{revision.version}</dd>
        </div>
        <div>
          <dt>Approved price</dt>
          <dd>{formatCurrency(revision.vehiclePrice)}</dd>
        </div>
        <div>
          <dt>Approved</dt>
          <dd>{formatMalaysianDateTime(publication.recordedAt)}</dd>
        </div>
        {withdrawal && (
          <div>
            <dt>Withdrawn</dt>
            <dd>{formatMalaysianDateTime(withdrawal.recordedAt)}</dd>
          </div>
        )}
      </dl>
      <div>
        <h4>Approved description</h4>
        <p>{revision.description}</p>
      </div>
      {signedReport && (
        <StaffLink
          prefetch={false}
          target="_blank"
          rel="noopener"
          href={`/dashboard/inspection/${signedReport.inspectionId}/reports/${signedReport.revision}`}
        >
          Open approved signed inspection report (new tab)
        </StaffLink>
      )}
      {withdrawal && (
        <div>
          <h4>Withdrawal reason</h4>
          <p>{withdrawal.reason}</p>
        </div>
      )}
    </section>
  );
}
