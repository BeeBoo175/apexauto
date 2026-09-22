import Image from "next/image";
import Link from "next/link";
import patterns from "@/components/layout/PublicPatterns.module.css";
import { formatMalaysianDate } from "@/lib/date-format";
import { categoryTitle, inspectionCategoryTitles } from "@/lib/guide/inspection-rules";
import { summariseInspectionAreas } from "@/lib/vehicles/inspection-summary";
import type { PublicVehicleDetail } from "@/lib/vehicles/public-detail";
import { vehicleTitle } from "@/lib/vehicles/vehicle-title";
import styles from "./LandingInspectionRecord.module.css";
import layout from "./VehicleLayout.module.css";

interface AreaSummary {
  code: string;
  label: string;
  total: number;
  passed: number;
  notFitted: number;
}

function summariseAreas(points: PublicVehicleDetail["checklist"]["points"]): AreaSummary[] {
  return summariseInspectionAreas(points).map((area) => ({
    code: area.code,
    label: categoryTitle(area.category),
    total: area.total,
    passed: area.passed,
    notFitted: area.notFitted,
  }));
}

// Exceptions go under the area name, so the score column stays one short figure and every row lines up.
function exceptionNote(area: AreaSummary) {
  const findings = area.total - area.passed - area.notFitted;
  const notes: string[] = [];
  if (area.notFitted > 0) {
    notes.push(`${area.notFitted} not fitted`);
  }
  if (findings > 0) {
    notes.push(`${findings} ${findings === 1 ? "finding" : "findings"}`);
  }
  return notes.join(", ");
}

// Stand-in areas for the loading silhouette; the real record has the same five rows.
const SHELL_AREAS: AreaSummary[] = Object.values(inspectionCategoryTitles).map((label, index) => ({
  code: `0${index + 1}`,
  label,
  total: 40,
  passed: 40,
  notFitted: 0,
}));

export function LandingInspectionRecord({
  detail,
  isLeadCar,
  loading = false,
}: {
  detail: PublicVehicleDetail | null;
  isLeadCar: boolean;
  // Shows the section with placeholder text until the record has loaded.
  loading?: boolean;
}) {
  if (!detail && !loading) {
    return null;
  }

  const listing = detail?.listing ?? null;
  const checklist = detail?.checklist ?? null;
  const name = listing ? vehicleTitle(listing) : "0000 Make Model Trim";
  const areas = checklist ? summariseAreas(checklist.points) : SHELL_AREAS;
  const signed = checklist ? formatMalaysianDate(checklist.signedAt) : "00 Month 0000";
  const grade = checklist ? checklist.grade : "A";

  return (
    <section
      className={`${patterns.band} ${patterns.bandPaper}`}
      aria-labelledby="inspection-record-heading"
      data-shell={loading || undefined}
    >
      <div className={`${layout.content} ${styles.proof}`}>
        <div className={`${patterns.photo} ${styles.media}`}>
          <Image
            src="/images/inspect-engine.jpg"
            alt="An open engine bay during inspection"
            fill
            sizes="(max-width: 920px) 100vw, 620px"
          />
        </div>
        <div className={styles.copy}>
          <div className={styles.intro}>
            <h2 id="inspection-record-heading" className={patterns.sectionTitle}>
              Signed inspection record
            </h2>
            <p className={styles.lead}>
              This is the record for the <strong>{name}</strong>
              {isLeadCar ? " above" : ""}, not a sample.
            </p>
          </div>
          <div className={`${patterns.sheet} ${styles.sheet}`}>
            <div className={styles.head}>
              <div className={styles.headTitle}>
                <span className={styles.headLabel}>Inspection record</span>
                <span className={styles.carName}>{name}</span>
              </div>
              {grade && (
                <span className={styles.grade}>
                  <span className={styles.gradeLabel}>Grade</span>
                  <span className={styles.gradeMark}>{grade}</span>
                </span>
              )}
            </div>
            <ul className={patterns.sheetRows}>
              {areas.map((area) => (
                <li key={`${area.code}-${area.label}`} className={styles.row}>
                  <span className={patterns.sheetCode}>{area.code}</span>
                  <span className={styles.areaName}>
                    {area.label}
                    {exceptionNote(area) && (
                      <span className={styles.exception}>{exceptionNote(area)}</span>
                    )}
                  </span>
                  <span className={styles.bar} aria-hidden="true">
                    <span
                      className={styles.barFill}
                      style={{ width: `${(area.passed / area.total) * 100}%` }}
                    />
                  </span>
                  <span className={styles.score}>
                    {area.passed}
                    <span className={styles.scoreTotal}>/{area.total}</span>
                  </span>
                </li>
              ))}
            </ul>
            <div className={patterns.sheetSign}>
              <span>
                Signed by the workshop technician, <span className={styles.signed}>{signed}</span>
              </span>
              <Link
                href={listing ? `/vehicles/${listing.id}#inspection` : "/vehicles"}
                tabIndex={loading ? -1 : undefined}
                prefetch={false}
                className={styles.cta}
              >
                Open the full record
              </Link>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
