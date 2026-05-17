import type { ReportPoint } from "@/lib/inspection/report-contracts";
import { formatCurrency } from "@/lib/currency-format";
import { label, severities } from "@/lib/inspection/contracts";
import styles from "./SignedReportFindings.module.css";

function bySeverityThenCode(first: ReportPoint, second: ReportPoint) {
  const firstRank = first.severity ? severities.indexOf(first.severity) : -1;
  const secondRank = second.severity ? severities.indexOf(second.severity) : -1;
  if (firstRank !== secondRank) {
    return secondRank - firstRank;
  }
  return first.code.localeCompare(second.code);
}

export function SignedReportFindings({
  inspectionId,
  findings,
}: {
  inspectionId: string;
  findings: ReportPoint[];
}) {
  return (
    <section className={styles.findings}>
      <h2>Findings</h2>
      {findings.length === 0 && <p>No findings were recorded. Every examined point passed.</p>}
      <ol>
        {[...findings].sort(bySeverityThenCode).map((point) => (
          <li className={styles.finding} key={point.code}>
            <div className={styles.heading}>
              <h3>{point.description}</h3>
              <p className={styles.estimate}>{formatCurrency(point.estimatedRepairCost)}</p>
            </div>
            <p className={styles.meta}>
              <span className={styles.code}>{point.code}</span>
              {label(point.category)}
              {point.severity && (
                <>
                  {" · "}
                  <span className={styles.severity} data-severity={point.severity}>
                    {label(point.severity)}
                  </span>
                </>
              )}
            </p>
            {point.notes && <p className={styles.notes}>{point.notes}</p>}
            {point.photos.length === 0 && <p className={styles.noPhoto}>No photo attached</p>}
            {point.photos.length > 0 && (
              <div className={styles.photos}>
                {point.photos.map((photo, photoIndex) => (
                  <figure key={photo.id}>
                    {/* Private evidence bypasses the shared image optimizer/cache. */}
                    {/* eslint-disable-next-line @next/next/no-img-element */}
                    <img
                      src={`/api/staff/inspection/drafts/${inspectionId}/photos/${photo.id}`}
                      alt={`${point.code} signed evidence ${photoIndex + 1}`}
                      loading="lazy"
                      width={photo.width}
                      height={photo.height}
                    />
                    <figcaption>Photo {photoIndex + 1}</figcaption>
                  </figure>
                ))}
              </div>
            )}
          </li>
        ))}
      </ol>
    </section>
  );
}
