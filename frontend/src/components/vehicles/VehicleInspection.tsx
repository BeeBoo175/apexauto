"use client";

import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { Input } from "@/components/ui/Input";
import { useVehicleInspection } from "@/hooks/use-vehicle-inspection";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDate } from "@/lib/date-format";
import { categoryTitle } from "@/lib/guide/inspection-rules";
import { inspectionCategoryIcons, label } from "@/lib/inspection/contracts";
import type { PublicVehicleDetail } from "@/lib/vehicles/public-detail";
import layout from "./VehicleLayout.module.css";
import styles from "./VehicleInspection.module.css";

export function VehicleInspection({ checklist }: { checklist: PublicVehicleDetail["checklist"] }) {
  const explorer = useVehicleInspection(checklist);
  const notFitted = checklist.points.filter((point) => point.kind === "NotFitted").length;

  return (
    <section aria-labelledby="vehicle-inspection" className={styles.inspection}>
      <div className={styles.introduction}>
        <header className={styles.heading}>
          <div>
            <h2 id="vehicle-inspection" className={layout.sectionHeading}>
              Inspection report
            </h2>
            <p>Signed inspection · {formatMalaysianDate(checklist.signedAt)}</p>
          </div>
          <div className={styles.grade}>
            <span>Inspection grade</span>
            <strong>{checklist.grade ?? "Ungraded"}</strong>
          </div>
        </header>
        <div className={styles.summary} aria-label="Inspection summary">
          <div>
            <strong>{checklist.points.length}</strong>
            <span>checkpoints in report</span>
          </div>
          <div data-tone="finding">
            <strong>{explorer.findings.length}</strong>
            <span>recorded findings</span>
          </div>
          <div>
            <strong>{notFitted}</strong>
            <span>items not fitted</span>
          </div>
        </div>
      </div>
      <p className={styles.explanation}>
        Open an area to read its checkpoints, or search for a part. Results reflect the signed
        inspection record.
      </p>

      <div className={`${styles.controls} ${layout.calmFields}`}>
        <Input
          label="Search inspection"
          id="inspection-search"
          type="search"
          placeholder="Try tyres, brakes or paint"
          value={explorer.search}
          onChange={(event) => explorer.setSearch(event.target.value)}
        />
        <Button
          type="button"
          variant="warning"
          size="filter"
          aria-pressed={explorer.findingsOnly}
          onClick={() => explorer.setFindingsOnly(!explorer.findingsOnly)}
        >
          Findings only ({explorer.findings.length})
        </Button>
      </div>

      <div className={styles.areas}>
        {explorer.areas.map((area) => {
          const panelId = `inspection-area-${area.category}`;
          return (
            <section key={area.category} className={styles.area} data-open={area.open || undefined}>
              <h3 className={styles.areaHeading}>
                <button
                  type="button"
                  className={styles.areaToggle}
                  aria-expanded={area.open}
                  aria-controls={panelId}
                  onClick={() => explorer.toggleArea(area.category)}
                >
                  <span className={styles.areaIcon}>
                    <Icon name={inspectionCategoryIcons[area.category] ?? "inspection"} />
                  </span>
                  <span className={styles.areaName}>
                    {categoryTitle(area.category)}
                    <small>
                      {area.total} checkpoints ·{" "}
                      <span data-tone={area.findingCount > 0 ? "finding" : undefined}>
                        {area.findingCount} {area.findingCount === 1 ? "finding" : "findings"}
                      </span>
                    </small>
                  </span>
                  <span className={styles.areaCaret}>
                    <Icon name="down" />
                  </span>
                </button>
              </h3>
              {area.open && (
                <ul id={panelId} className={styles.points}>
                  {area.points.map((point) => (
                    <li
                      key={point.code}
                      data-tone={point.kind === "Finding" ? "finding" : "neutral"}
                      data-severity={point.severity ?? undefined}
                      data-kind={point.kind ?? "unrecorded"}
                    >
                      <span className={styles.pointCode}>{point.code}</span>
                      <span className={styles.pointText}>
                        <strong>{point.description}</strong>
                        <span className={styles.condition}>
                          {point.kind === null ? "No finding recorded" : label(point.kind)}
                          {point.severity && ` · ${label(point.severity)}`}
                          {point.estimatedRepairCost > 0 &&
                            ` · Estimated repair ${formatCurrency(point.estimatedRepairCost)}`}
                        </span>
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </section>
          );
        })}

        {explorer.areas.length === 0 && (
          <div className={styles.empty}>
            <h3>No matching checkpoints</h3>
            <p>Try a different part or search across all areas.</p>
            <Button variant="secondary" onClick={explorer.reset}>
              Clear inspection filters
            </Button>
          </div>
        )}
      </div>
    </section>
  );
}
