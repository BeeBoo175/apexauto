"use client";
import { useState } from "react";
import { SearchField } from "@/components/ui/SearchField";
import { label, type Draft } from "@/lib/inspection/contracts";
import type { ChecklistDraft } from "@/lib/inspection/draft";
import { ChecklistPoint } from "./ChecklistPoint";
import styles from "./Inspection.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";

export function InspectionChecklist({
  draft,
  value,
  actorId,
  editable,
  disabled,
  onChange,
  onLock,
}: {
  draft: Draft;
  value: ChecklistDraft;
  actorId: string;
  editable: boolean;
  disabled: boolean;
  onChange: (v: ChecklistDraft) => void;
  onLock: (locked: boolean) => void;
}) {
  const [search, setSearch] = useState("");
  const categories = [...new Set(draft.catalog.map((point) => point.category))];
  const fitted = draft.catalog.filter((point) => value.entries[point.code]?.kind !== "NotFitted");
  const allExamined = fitted.every((point) => value.examined.includes(point.code));
  return (
    <section
      className={`${styles.section} ${workbench.protocol}`}
      aria-label="Inspection checklist"
    >
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
      {categories.map((category) => {
        const points = draft.catalog.filter(
          (point) =>
            point.category === category &&
            `${point.code} ${point.description}`.toLowerCase().includes(search.toLowerCase()),
        );
        if (!points.length) {
          return null;
        }
        return (
          <details className={styles.category} key={category} open={search ? true : undefined}>
            <summary>
              {label(category)}
              <span>{points.length} points</span>
            </summary>
            {points.map((point) => (
              <ChecklistPoint
                key={point.code}
                point={point}
                draft={draft}
                value={value}
                actorId={actorId}
                editable={editable}
                disabled={disabled}
                onChange={onChange}
                onLock={onLock}
              />
            ))}
          </details>
        );
      })}
      {!draft.catalog.some((point) =>
        `${point.code} ${point.description}`.toLowerCase().includes(search.toLowerCase()),
      ) && <p>No checklist points match your search.</p>}
    </section>
  );
}
