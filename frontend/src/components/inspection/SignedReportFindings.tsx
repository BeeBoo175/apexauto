import type { ReportPoint } from "@/lib/inspection/report-contracts";
import { formatCurrency } from "@/lib/currency-format";
import { label } from "@/lib/inspection/contracts";
import { InspectionPointStatus } from "./WorkshopStatus";
import styles from "./SignedReportFindings.module.css";

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
      {findings.map((point) => (
        <article className={styles.finding} key={point.code}>
          <header>
            <div>
              <span className={styles.code}>{point.code}</span>
              <span className={styles.category}>{label(point.category)}</span>
            </div>
            <InspectionPointStatus kind={point.kind} severity={point.severity} />
          </header>
          <h3>{point.description}</h3>
          {point.notes && <p>{point.notes}</p>}
          <dl>
            <dt>Original estimate</dt>
            <dd>{formatCurrency(point.estimatedRepairCost)}</dd>
          </dl>
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
                  <figcaption>
                    {point.code} · Photo {photoIndex + 1}
                  </figcaption>
                </figure>
              ))}
            </div>
          )}
        </article>
      ))}
    </section>
  );
}
