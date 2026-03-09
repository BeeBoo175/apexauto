"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Select } from "@/components/ui/Input";
import { StaffLink } from "@/components/staff/StaffLink";
import { DataTablePagination } from "@/components/ui/data-table/DataTablePagination";
import { useInspectionHistory } from "@/hooks/use-inspection-history";
import { parseRevisions, parseRepairs } from "@/lib/inspection/parse-report";
import { inspectionTimeline } from "@/lib/inspection/timeline";
import type { EditorSnapshot } from "@/lib/inspection/editor-snapshot";
import type { RepairSelection } from "@/lib/inspection/query";
import { formatMalaysianDateTime } from "@/lib/date-format";
import styles from "./InspectionTimeline.module.css";
import { InspectionPreviousWork } from "./InspectionPreviousWork";

export function InspectionTimeline({
  snapshot,
  disabled,
  onSelect,
}: {
  snapshot: EditorSnapshot;
  disabled: boolean;
  onSelect: (selection: RepairSelection) => void;
}) {
  const id = snapshot.draft.id;
  const [previousRevision, setPreviousRevision] = useState(0);
  const revisions = useInspectionHistory(
    snapshot.revisions,
    `reports/${id}/revisions`,
    parseRevisions,
    "Revision history could not be refreshed. Existing revisions remain shown.",
  );
  const repairs = useInspectionHistory(
    snapshot.repairs,
    `reports/${id}/revisions/${snapshot.draft.revision}/repair-history`,
    parseRepairs,
    "Repair history could not be refreshed. Existing receipts remain shown.",
  );
  const events = revisions.history ? inspectionTimeline(revisions.history, repairs.history) : [];
  const busy = revisions.busy || repairs.busy;
  return (
    <section className={styles.history} aria-label="Record history" aria-busy={busy}>
      <div className={styles.heading}>
        <div>
          <h2>Record history</h2>
          <p>Signed revisions and current-revision repair receipts, newest first.</p>
        </div>
        <span>{events.length} loaded events</span>
      </div>
      {!events.length && <p>No retained events on these pages.</p>}
      <ol className={styles.timeline}>
        {events.map((event) => (
          <li key={`${event.title}:${event.id}`}>
            <time dateTime={event.at}>{formatMalaysianDateTime(event.at)}</time>
            <strong>{event.title}</strong>
            <span className={styles.actor}>{event.actor}</span>
            <details>
              <summary>View details</summary>
              <p>{event.detail}</p>
              <p className={styles.reference}>Receipt: {event.id}</p>
              {event.request ? (
                <Button
                  size="compact"
                  variant="secondary"
                  disabled={disabled || busy}
                  onClick={() => {
                    if (event.request) {
                      onSelect(event.request);
                    }
                  }}
                >
                  Open exact repair request
                </Button>
              ) : (
                <StaffLink
                  prefetch={false}
                  target="_blank"
                  rel="noopener"
                  href={`/dashboard/inspection/${id}/reports/${event.revision}`}
                >
                  Open signed revision {event.revision}
                </StaffLink>
              )}
            </details>
          </li>
        ))}
      </ol>
      {revisions.history?.items.some((revision) => revision.revision < snapshot.draft.revision) && (
        <Select
          label="Previous work revision"
          density="compact"
          value={previousRevision}
          disabled={disabled || busy}
          onChange={(event) => setPreviousRevision(Number(event.target.value))}
        >
          <option value={0}>Choose a signed revision</option>
          {revisions.history.items
            .filter((revision) => revision.revision < snapshot.draft.revision)
            .map((revision) => (
              <option key={revision.revision} value={revision.revision}>
                Revision {revision.revision}
              </option>
            ))}
        </Select>
      )}
      {previousRevision > 0 && (
        <InspectionPreviousWork
          key={`${id}:${previousRevision}`}
          inspectionId={id}
          revision={previousRevision}
          disabled={disabled}
          onSelect={onSelect}
        />
      )}
      {revisions.history && (
        <section className={styles.paging} aria-label="Signed revision pages">
          <h3>Signed revision pages</h3>
          <DataTablePagination
            page={{ ...revisions.history, total: revisions.history.totalCount }}
            count={revisions.history.items.length}
            disabled={disabled || busy}
            onPage={(page, size) => void revisions.load(page, size)}
          />
        </section>
      )}
      {repairs.history && (
        <section className={styles.paging} aria-label="Repair request pages">
          <h3>Repair request pages · revision {repairs.history.revision}</h3>
          <DataTablePagination
            page={{ ...repairs.history, total: repairs.history.totalCount }}
            count={repairs.history.items.length}
            disabled={disabled || busy}
            onPage={(page, size) => void repairs.load(page, size)}
          />
        </section>
      )}
    </section>
  );
}
