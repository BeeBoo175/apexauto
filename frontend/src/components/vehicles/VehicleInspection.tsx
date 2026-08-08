"use client";

import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { Input } from "@/components/ui/Input";
import { useVehicleInspection } from "@/hooks/use-vehicle-inspection";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDate } from "@/lib/date-format";
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
        Explore by area or search for a part. Results reflect the signed inspection record.
      </p>
      <div className={styles.explorer}>
        <nav className={styles.categories} aria-label="Inspection categories">
          <Button
            type="button"
            variant="choice"
            size="tile"
            aria-pressed={explorer.category === "all"}
            onClick={() => explorer.setCategory("all")}
          >
            <Icon name="inspection" />
            <span>
              All areas<small>{checklist.points.length} checkpoints</small>
            </span>
          </Button>
          {explorer.categories.map((category) => {
            const points = checklist.points.filter((point) => point.category === category);
            const count = points.filter((point) => point.kind === "Finding").length;
            return (
              <Button
                key={category}
                type="button"
                variant="choice"
                size="tile"
                aria-pressed={explorer.category === category}
                onClick={() => explorer.setCategory(category)}
              >
                <Icon name={inspectionCategoryIcons[category] ?? "inspection"} />
                <span>
                  {label(category)}
                  <small>
                    {points.length} checkpoints · {count} findings
                  </small>
                </span>
              </Button>
            );
          })}
        </nav>
        <div className={styles.content}>
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
          <p className={styles.resultCount} role="status">
            {explorer.visible.size} matching checkpoints
          </p>
          <div
            className={styles.results}
            tabIndex={0}
            role="region"
            aria-label="Inspection checkpoints"
          >
            <ul>
              {checklist.points.map((point) => (
                <li
                  key={point.code}
                  hidden={!explorer.visible.has(point.code)}
                  data-tone={point.kind === "Finding" ? "finding" : "neutral"}
                  data-severity={point.severity ?? undefined}
                  data-kind={point.kind ?? "unrecorded"}
                >
                  <div className={styles.pointMeta}>
                    <span>{point.code}</span>
                    <span>{label(point.category)}</span>
                  </div>
                  <h3>{point.description}</h3>
                  <p className={styles.condition}>
                    {point.kind === null ? "No finding recorded" : label(point.kind)}
                    {point.severity && ` · ${label(point.severity)}`}
                  </p>
                  {point.estimatedRepairCost > 0 && (
                    <p>Estimated repair: {formatCurrency(point.estimatedRepairCost)}</p>
                  )}
                </li>
              ))}
            </ul>
            {explorer.visible.size === 0 && (
              <div className={styles.empty}>
                <h3>No matching checkpoints</h3>
                <p>Try a different part or search across all areas.</p>
                <Button variant="secondary" onClick={explorer.reset}>
                  Clear inspection filters
                </Button>
              </div>
            )}
          </div>
        </div>
      </div>
    </section>
  );
}
