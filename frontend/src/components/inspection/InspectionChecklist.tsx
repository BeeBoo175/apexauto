"use client";
import { useState, type ReactNode } from "react";
import { Button } from "@/components/ui/Button";
import { Badge } from "@/components/ui/Badge";
import { SearchField } from "@/components/ui/SearchField";
import { useInspectionPhoto } from "@/hooks/use-inspection-photo";
import { useInspectionChecklistScroll } from "@/hooks/use-inspection-checklist-scroll";
import { label, type Draft } from "@/lib/inspection/contracts";
import { checklistViews, type ChecklistDraft, type ChecklistView } from "@/lib/inspection/draft";
import type { Report } from "@/lib/inspection/report-contracts";
import { ChecklistPoint } from "./ChecklistPoint";
import { InspectionPointStatus } from "./WorkshopStatus";
import styles from "./Inspection.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import workshop from "./Workshop.module.css";

export function InspectionChecklist({
  draft,
  value,
  actorId,
  editable,
  disabled,
  onChange,
  onLock,
  active = true,
  report,
  managerControls,
}: {
  draft: Draft;
  value: ChecklistDraft;
  actorId: string;
  editable: boolean;
  disabled: boolean;
  onChange: (v: ChecklistDraft) => void;
  onLock: (locked: boolean) => void;
  active?: boolean;
  report?: Report | null;
  managerControls?: ReactNode;
}) {
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState<ChecklistView>("all");
  const [selectedCode, setSelectedCode] = useState(draft.catalog[0]?.code ?? "");
  const views = checklistViews(draft.catalog, value, search);
  const matches = views[filter];
  const selected = matches.find((point) => point.code === selectedCode) ?? matches[0];
  const pointCode = selected?.code ?? selectedCode;
  const { sectionRef, toolsRef, selectedRef, selectedHidden, scrollDirection, showSelected } =
    useInspectionChecklistScroll(active, selected?.code ?? "");
  const photo = useInspectionPhoto(
    draft.id,
    draft.assignmentVersion,
    pointCode,
    actorId,
    (receipt) =>
      onChange({
        ...value,
        photos: {
          ...value.photos,
          [pointCode]: [...(value.photos[pointCode] ?? []), receipt.id],
        },
        examined: value.examined.filter((code) => code !== pointCode),
      }),
    onLock,
  );
  const categories = [...new Set(draft.catalog.map((point) => point.category))];
  const fitted = draft.catalog.filter((point) => value.entries[point.code]?.kind !== "NotFitted");
  const allExamined = fitted.every((point) => value.examined.includes(point.code));
  let jumpArrow = "↑";
  let jumpPrefix = "Jump to ";
  if (scrollDirection === "below") {
    jumpArrow = "↓";
  } else if (scrollDirection === "collapsed") {
    jumpArrow = "↗";
    jumpPrefix = "Expand ";
  }
  return (
    <section
      ref={sectionRef}
      className={`${styles.section} ${styles.checklist}`}
      aria-label="Inspection checklist"
      hidden={!active}
    >
      <div ref={toolsRef} className={workbench.stickySectionHeader}>
        <div className={`${workbench.sectionHeader} ${styles.checklistHeading}`}>
          <h2 className={workbench.sectionTitle}>Inspection checklist</h2>
          <div
            className={styles.selectionAction}
            data-visible={selectedHidden && Boolean(selected)}
          >
            <Button
              size="compact"
              variant="secondary"
              className={styles.jumpHeaderButton}
              aria-label={`Show selected point ${selected?.code ?? ""}`}
              disabled={!selectedHidden || !selected}
              onClick={showSelected}
            >
              <span className={styles.jumpArrow} aria-hidden="true">
                {jumpArrow}
              </span>
              <span>
                {jumpPrefix}
                <strong>{selected?.code}</strong>
              </span>
            </Button>
          </div>
        </div>
        <div className={styles.checklistTools}>
          <SearchField
            label="Find a checklist point"
            placeholder="Find a point or description"
            value={search}
            disabled={disabled}
            onChange={setSearch}
            onClear={search ? () => setSearch("") : undefined}
          />
          <div className={styles.checklistViews} aria-label="Checklist views">
            {(["all", "findings", "remaining"] as const).map((view) => (
              <Button
                key={view}
                size="compact"
                variant="secondary"
                className={styles.checklistView}
                aria-pressed={filter === view}
                disabled={photo.file !== null || photo.busy || photo.uncertain}
                onClick={() => setFilter(view)}
              >
                {label(view[0].toUpperCase() + view.slice(1))}
                <Badge variant="count" data-selected={filter === view}>
                  {views[view].length}
                  <span className="sr-only">
                    {search && " matching"}
                    {views[view].length === 1 ? " point" : " points"}
                  </span>
                </Badge>
              </Button>
            ))}
          </div>
          {editable && (
            <label className={styles.checkbox}>
              <input
                type="checkbox"
                checked={allExamined}
                disabled={disabled}
                onChange={(e) =>
                  onChange({
                    ...value,
                    examined: e.target.checked ? fitted.map((point) => point.code) : [],
                  })
                }
              />
              I have examined all {fitted.length} fitted points
            </label>
          )}
        </div>
      </div>
      <div className={styles.managerControls}>{managerControls}</div>
      {active && (
        <div className={workshop.split}>
          <nav className={workshop.index} aria-label="Inspection points">
            {categories.map((category) => {
              const points = matches.filter((point) => point.category === category);
              if (!points.length) {
                return null;
              }
              return (
                <details
                  className={workshop.indexGroup}
                  key={category}
                  open={
                    search || filter !== "all" || selected?.category === category ? true : undefined
                  }
                >
                  <summary>{label(category)}</summary>
                  {points.map((point) => (
                    <Button
                      key={point.code}
                      ref={selected?.code === point.code ? selectedRef : undefined}
                      size="compact"
                      variant="ghost"
                      className={workshop.indexItem}
                      aria-pressed={selected?.code === point.code}
                      disabled={photo.file !== null || photo.busy || photo.uncertain}
                      onClick={() => setSelectedCode(point.code)}
                    >
                      <span className={workshop.pointCode}>{point.code}</span>
                      <strong>{point.description}</strong>
                      <InspectionPointStatus
                        kind={value.entries[point.code]?.kind}
                        severity={value.entries[point.code]?.severity}
                        examined={value.examined.includes(point.code)}
                        compact
                      />
                    </Button>
                  ))}
                </details>
              );
            })}
            {!matches.length && <p>No checklist points match your search.</p>}
          </nav>
          <div className={workshop.detail}>
            <p className={styles.help}>
              {draft.isSigned ? "Signed findings · read only" : "Current inspection draft"}
            </p>
            {selected && (
              <ChecklistPoint
                point={selected}
                draft={draft}
                value={value}
                photo={photo}
                editable={editable}
                disabled={disabled}
                onChange={onChange}
                signedPoint={report?.checklist.find((point) => point.code === selected.code)}
              />
            )}
          </div>
        </div>
      )}
    </section>
  );
}
