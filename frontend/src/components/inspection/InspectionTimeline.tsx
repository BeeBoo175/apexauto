"use client";

import { Select } from "@/components/ui/Input";
import { StaffLink } from "@/components/staff/StaffLink";
import type { useInspectionTimeline } from "@/hooks/use-inspection-timeline";
import { inspectionTimeline } from "@/lib/inspection/timeline";
import type { EditorSnapshot } from "@/lib/inspection/editor-snapshot";
import type { RepairSelection } from "@/lib/inspection/query";
import { formatMalaysianDateTime } from "@/lib/date-format";
import styles from "./InspectionTimeline.module.css";
import { InspectionPreviousWork } from "./InspectionPreviousWork";
import { InspectionHistoryProgress, InspectionRepairLink } from "./InspectionHistory";

export function InspectionTimeline({
  snapshot,
  timeline,
  disabled,
  onSelect,
}: {
  snapshot: EditorSnapshot;
  timeline: ReturnType<typeof useInspectionTimeline>;
  disabled: boolean;
  onSelect: (selection: RepairSelection) => void;
}) {
  const id = snapshot.draft.id;
  const { revisions, repairs, previous, previousRevision, selectRevision } = timeline;
  const events = inspectionTimeline(revisions.history, repairs.history);
  const busy = revisions.busy || repairs.busy;
  const loaded = revisions.history && (!snapshot.draft.isSigned || repairs.history);
  return (
    <section className={styles.history} aria-label="Record history" aria-busy={busy}>
      <div className={styles.heading}>
        <div>
          <h2>Record history</h2>
          <p>Signed revisions and current-revision repair receipts, newest first.</p>
        </div>
        {events.length > 0 && <span>{events.length} loaded events</span>}
      </div>
      {loaded && !events.length && <p>No retained events.</p>}
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
                <InspectionRepairLink
                  inspectionId={id}
                  selection={event.request}
                  disabled={disabled}
                  onSelect={onSelect}
                />
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
          disabled={disabled}
          onChange={(event) => selectRevision(Number(event.target.value))}
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
          revision={previousRevision}
          read={previous}
          disabled={disabled}
          onSelect={onSelect}
        />
      )}
      <InspectionHistoryProgress read={revisions} label="Signed revisions" disabled={disabled} />
      {snapshot.draft.isSigned && (
        <InspectionHistoryProgress read={repairs} label="Repair requests" disabled={disabled} />
      )}
    </section>
  );
}
