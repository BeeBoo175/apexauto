import type { Report } from "@/lib/inspection/report-contracts";
import { label } from "@/lib/inspection/contracts";
import styles from "./SignedReportChecklist.module.css";

export function SignedReportChecklist({ report }: { report: Report }) {
  const categories = [...new Set(report.checklist.map((point) => point.category))];
  return (
    <section className={styles.checklist}>
      <h2>Remaining checklist</h2>
      <p>Points examined without a finding. Findings are listed above.</p>
      {categories.map((category) => {
        const points = report.checklist.filter(
          (point) => point.category === category && point.kind !== "Finding",
        );
        if (points.length === 0) {
          return null;
        }
        return (
          <section className={styles.category} key={category}>
            <header>
              <h3>{label(category)}</h3>
              <span>{points.length} points</span>
            </header>
            <ul>
              {points.map((point) => (
                <li key={point.code}>
                  <span className={styles.code}>{point.code}</span>
                  {point.description}
                  {point.kind === "NotFitted" && <em> · Not fitted</em>}
                </li>
              ))}
            </ul>
          </section>
        );
      })}
    </section>
  );
}
