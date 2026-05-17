import { reportDocumentHref } from "@/lib/inspection/contracts";
import { StaffLink } from "@/components/staff/StaffLink";
import type { ListingRevision, ListingWorkspace } from "@/lib/vehicles/listing-workspace";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import styles from "./ListingPublication.module.css";

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
    return (
      <section className={styles.section} aria-label="Publication record">
        <h3>Publication record</h3>
        <p className={styles.help}>No publication has been approved.</p>
      </section>
    );
  }

  return (
    <section className={styles.section} aria-label="Approved publication record">
      <div className={styles.sectionHeading}>
        <h3>Approved publication</h3>
        <span className={styles.metadata}>Version {revision.version}</span>
      </div>
      <dl className={styles.facts}>
        <div className={styles.recordPrice}>
          <dt>Approved price</dt>
          <dd>{formatCurrency(revision.vehiclePrice)}</dd>
        </div>
        <div>
          <dt>Approved</dt>
          <dd>
            <time dateTime={publication.recordedAt}>
              {formatMalaysianDateTime(publication.recordedAt)}
            </time>
          </dd>
        </div>
      </dl>
      <div className={styles.recordText} aria-label="Published description">
        <h4>Description</h4>
        <p>{revision.description}</p>
      </div>
      {signedReport && (
        <StaffLink
          prefetch={false}
          target="_blank"
          rel="noopener"
          className={styles.reportLink}
          href={reportDocumentHref(signedReport.inspectionId, signedReport.revision)}
        >
          Open approved signed inspection report (new tab)
        </StaffLink>
      )}
      {withdrawal && (
        <div className={styles.withdrawalRecord}>
          <div className={styles.sectionHeading}>
            <h3>Withdrawal</h3>
            <time className={styles.metadata} dateTime={withdrawal.recordedAt}>
              {formatMalaysianDateTime(withdrawal.recordedAt)}
            </time>
          </div>
          <div className={styles.recordText}>
            <h4>Reason</h4>
            <p>{withdrawal.reason}</p>
          </div>
        </div>
      )}
    </section>
  );
}
