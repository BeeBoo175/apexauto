import type { Report } from "@/lib/inspection/report-contracts";
import { formatCurrency } from "@/lib/currency-format";
import { label } from "@/lib/inspection/contracts";
import styles from "./SignedReport.module.css";

export function SignedReportChecklist({ report }: { report: Report }) {
  const categories = [...new Set(report.checklist.map((point) => point.category))];
  return (
    <>
      <nav className={styles.categories} aria-label="Signed checklist categories">
        {categories.map((category, index) => (
          <a key={category} href={`#report-category-${index}`}>
            {label(category)}
          </a>
        ))}
      </nav>
      {categories.map((category, index) => (
        <section className={styles.category} key={category} id={`report-category-${index}`}>
          <header className={styles.categoryHeading}>
            <h2>{label(category)}</h2>
            <span>
              {report.checklist.filter((point) => point.category === category).length} points
            </span>
          </header>
          <div className={styles.checklistScroll}>
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
                            {point.photos.map((photo, photoIndex) => (
                              <figure key={photo.id}>
                                {/* Private evidence bypasses the shared image optimizer/cache. */}
                                {/* eslint-disable-next-line @next/next/no-img-element */}
                                <img
                                  src={`/api/staff/inspection/drafts/${report.inspectionId}/photos/${photo.id}`}
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
                      </td>
                      <td>
                        {point.kind === "Finding" ? formatCurrency(point.estimatedRepairCost) : "—"}
                      </td>
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
        </section>
      ))}
    </>
  );
}
