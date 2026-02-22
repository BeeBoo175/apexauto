import type { Report } from "@/lib/inspection/report-contracts";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { label } from "@/lib/inspection/contracts";
import { StaffLink } from "@/components/staff/StaffLink";
import styles from "./SignedReport.module.css";

export function SignedReport({ report }: { report: Report }) {
  const { vehicle } = report;
  const categories = [...new Set(report.checklist.map((point) => point.category))];
  return (
    <div data-inspection-report>
      <div className={styles.controls}></div>
      <article className={styles.report}>
        <div className={styles.controls}>
          <StaffLink prefetch={false} href={`/dashboard/inspection/${report.inspectionId}`}>
            Back to inspection
          </StaffLink>
        </div>
        <header className={styles.heading}>
          <div>
            <h1>Inspection report</h1>
            <p>
              {vehicle.registrationPlate} · {vehicle.make} {vehicle.model} {vehicle.trim}
            </p>
          </div>
          <strong>
            {report.assessment.isRejected ? "Rejected" : `Grade ${report.assessment.grade}`}
          </strong>
        </header>
        <dl className={styles.metadata}>
          <div>
            <dt>Inspection</dt>
            <dd>{report.inspectionId}</dd>
          </div>
          <div>
            <dt>Signed revision</dt>
            <dd>{report.revision}</dd>
          </div>
          <div>
            <dt>VIN</dt>
            <dd>{vehicle.vin}</dd>
          </div>
          <div>
            <dt>Manufactured</dt>
            <dd>{vehicle.manufactureYear}</dd>
          </div>
          <div>
            <dt>Recorded mileage</dt>
            <dd>{vehicle.mileageKilometres.toLocaleString("en-MY")} km</dd>
          </div>
          <div>
            <dt>Original repair estimate</dt>
            <dd>{formatCurrency(report.assessment.estimatedRepairCost)}</dd>
          </div>
          <div>
            <dt>Technician</dt>
            <dd>{report.technicianIdentity}</dd>
          </div>
          <div>
            <dt>Signed (Malaysia)</dt>
            <dd>{formatMalaysianDateTime(report.signedAt)}</dd>
          </div>
          <div>
            <dt>Catalog</dt>
            <dd>{report.catalogVersion}</dd>
          </div>
          <div>
            <dt>Saved / assignment versions</dt>
            <dd>
              {report.saveVersion} / {report.assignmentVersion}
            </dd>
          </div>
          <div>
            <dt>Intake appointment</dt>
            <dd>{vehicle.intakeAppointmentId}</dd>
          </div>
          <div>
            <dt>Checked in (Malaysia)</dt>
            <dd>{formatMalaysianDateTime(vehicle.checkedInAt)}</dd>
          </div>
        </dl>
        <p className={styles.explanation}>
          This is the retained signed revision. Linked repair work does not change its original
          findings, estimates or grade.
        </p>
        {categories.map((category) => (
          <section className={styles.category} key={category}>
            <h2>{label(category)}</h2>
            <table>
              <caption className="sr-only">{label(category)} signed checklist</caption>
              <thead>
                <tr>
                  <th scope="col">Point</th>
                  <th scope="col">Disposition & notes</th>
                  <th scope="col">Original estimate</th>
                </tr>
              </thead>
              <tbody>
                {report.checklist
                  .filter((point) => point.category === category)
                  .map((point) => (
                    <tr key={point.code}>
                      <th scope="row">
                        <span>{point.code}</span>
                        {point.description}
                      </th>
                      <td>
                        <strong>{point.kind === null ? "Passed" : label(point.kind)}</strong>
                        {point.severity && <span> · {label(point.severity)}</span>}
                        {point.notes && <p>{point.notes}</p>}
                        {point.photos.length > 0 && (
                          <div className={styles.photos}>
                            {point.photos.map((photo, index) => (
                              <figure key={photo.id}>
                                {/* Private signed evidence bypasses the shared image optimizer/cache. */}
                                {/* eslint-disable-next-line @next/next/no-img-element */}
                                <img
                                  src={`/api/staff/inspection/drafts/${report.inspectionId}/photos/${photo.id}`}
                                  alt={`${point.code} signed evidence ${index + 1}`}
                                  loading="lazy"
                                  width={photo.width}
                                  height={photo.height}
                                />
                                <figcaption>
                                  {point.code} · Photo {index + 1}
                                </figcaption>
                              </figure>
                            ))}
                          </div>
                        )}
                      </td>
                      <td>
                        {point.kind === "Finding" ? formatCurrency(point.estimatedRepairCost) : "—"}
                      </td>
                    </tr>
                  ))}
              </tbody>
            </table>
          </section>
        ))}
        <footer className={styles.footer}>
          Signed by {report.technicianIdentity} · {formatMalaysianDateTime(report.signedAt)} ·
          Revision receipt {report.operationId}
        </footer>

        <div className={styles.controls}></div>
      </article>
    </div>
  );
}
