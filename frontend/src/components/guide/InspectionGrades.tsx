import patterns from "@/components/layout/PublicPatterns.module.css";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import type { InspectionGradingPolicy } from "@/lib/guide/inspection-points";
import type { GuideWarranty } from "@/lib/guide/purchase-guide";
import { InspectionGradeRuler } from "./InspectionGradeRuler";
import styles from "./InspectionGrades.module.css";

export function InspectionGrades({
  policy,
  warranties,
}: {
  policy: InspectionGradingPolicy;
  warranties: readonly GuideWarranty[];
}) {
  return (
    <section
      id="grades"
      className={`${patterns.band} ${patterns.bandTone}`}
      aria-labelledby="inspection-grades-heading"
    >
      <div className={`${layout.content} ${styles.grades}`}>
        <h2 id="inspection-grades-heading" className={layout.sectionHeading}>
          How a car is graded
        </h2>
        <div className={styles.card}>
          <InspectionGradeRuler policy={policy} warranties={warranties} />
        </div>
      </div>
    </section>
  );
}
