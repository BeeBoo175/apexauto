"use client";
import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { SearchField } from "@/components/ui/SearchField";
import { useInspectionPhoto } from "@/hooks/use-inspection-photo";
import { label, type Draft } from "@/lib/inspection/contracts";
import type { ChecklistDraft } from "@/lib/inspection/draft";
import type { Report } from "@/lib/inspection/report-contracts";
import { ChecklistPoint } from "./ChecklistPoint";
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
}) {
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState<"all" | "findings" | "remaining">("all");
  const [selectedCode, setSelectedCode] = useState(draft.catalog[0]?.code ?? "");
  const matches = draft.catalog.filter((point) => {
    if (filter === "findings" && value.entries[point.code]?.kind !== "Finding") {
      return false;
    }
    if (filter === "remaining" && value.examined.includes(point.code)) {
      return false;
    }
    return `${point.code} ${point.description} ${point.category}`
      .toLowerCase()
      .includes(search.toLowerCase());
  });
  const selected = matches.find((point) => point.code === selectedCode) ?? matches[0];
  const pointCode = selected?.code ?? selectedCode;
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
  if (!active) {
    return null;
  }
  return (
    <section className={styles.section} aria-label="Inspection checklist">
      <div className={workbench.toolbar}>
        <h2 className={workbench.sectionTitle}>Inspection checklist</h2>
        <span className={workbench.help}>
          {draft.catalog.length} points · {draft.catalogVersion}
        </span>
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
        <div className={workshop.footerActions} aria-label="Checklist views">
          {(["all", "findings", "remaining"] as const).map((view) => (
            <Button
              key={view}
              size="compact"
              variant="secondary"
              aria-pressed={filter === view}
              disabled={photo.file !== null || photo.busy || photo.uncertain}
              onClick={() => setFilter(view)}
            >
              {label(view[0].toUpperCase() + view.slice(1))}
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
      {editable && (
        <p className={styles.help}>
          Record each exception and examine every fitted point before signing. Save preserves
          exceptions; a partial examination remains unattested.
        </p>
      )}
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
                    size="compact"
                    variant="ghost"
                    className={workshop.indexItem}
                    aria-pressed={selected?.code === point.code}
                    disabled={photo.file !== null || photo.busy || photo.uncertain}
                    onClick={() => setSelectedCode(point.code)}
                  >
                    <span className={workshop.pointCode}>{point.code}</span>
                    <strong>{point.description}</strong>
                    <span className={workshop.pointState}>
                      {label(
                        value.entries[point.code]?.severity ??
                          value.entries[point.code]?.kind ??
                          (value.examined.includes(point.code) ? "Passed" : "Not examined"),
                      )}
                      {value.examined.includes(point.code) ? " · Examined" : ""}
                    </span>
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
    </section>
  );
}
