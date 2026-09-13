import Image from "next/image";
import Link from "next/link";
import patterns from "@/components/layout/PublicPatterns.module.css";
import { formatMalaysianDate } from "@/lib/date-format";
import { inspectionCategoryTitles } from "@/lib/guide/inspection-rules";
import type { PublicVehicleDetail } from "@/lib/vehicles/public-detail";
import styles from "./LandingInspectionRecord.module.css";
import layout from "./VehicleLayout.module.css";

interface AreaSummary {
  code: string;
  label: string;
  total: number;
  passed: number;
  notFitted: number;
}

function categoryTitle(category: string) {
  const titles: Record<string, string> = inspectionCategoryTitles;
  return titles[category] ?? category;
}

function summariseAreas(points: PublicVehicleDetail["checklist"]["points"]): AreaSummary[] {
  const areas = new Map<string, AreaSummary>();

  for (const point of points) {
    const area = areas.get(point.category) ?? {
      code: point.code.split("-")[0],
      label: categoryTitle(point.category),
      total: 0,
      passed: 0,
      notFitted: 0,
    };
    area.total += 1;
    if (point.kind === "NotFitted") {
      area.notFitted += 1;
    }
    if (point.kind === null) {
      area.passed += 1;
    }
    areas.set(point.category, area);
  }

  return [...areas.values()];
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

export function LandingInspectionRecord({
  detail,
  isLeadCar,
}: {
  detail: PublicVehicleDetail | null;
  isLeadCar: boolean;
}) {
  if (!detail) {
    return null;
  }

  const { listing, checklist } = detail;
  const name = `${listing.manufactureYear} ${listing.make} ${listing.model} ${listing.trim}`;
  const areas = summariseAreas(checklist.points);
  const signed = formatMalaysianDate(checklist.signedAt);

  return (
    <section
      className={`${patterns.band} ${patterns.bandPaper}`}
      aria-labelledby="inspection-record-heading"
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
              {checklist.grade && (
                <span className={styles.grade}>
                  <span className={styles.gradeLabel}>Grade</span>
                  <span className={styles.gradeMark}>{checklist.grade}</span>
                </span>
              )}
            </div>
            <ul className={patterns.sheetRows}>
              {areas.map((area) => (
                <li key={area.label} className={styles.row}>
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
              <span>Signed by the workshop technician, {signed}</span>
              <Link
                href={`/vehicles/${listing.id}#inspection`}
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
