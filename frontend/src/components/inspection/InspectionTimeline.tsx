"use client";

import { Button } from "@/components/ui/Button";
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
  const selectedRepairs = previousRevision === snapshot.draft.revision ? repairs : previous;
  const hasRepairs = (revision: number) =>
    revisions.history?.items.some((item) => item.revision === revision && item.hasRepairRequests);
  const loaded = revisions.history && (!snapshot.draft.isSigned || repairs.history);
  return (
    <section className={styles.history} aria-label="Record history" aria-busy={busy}>
      <div className={styles.heading}>
        <div>
          <h2>Record history</h2>
          <p>Inspections and repair requests, newest first.</p>
        </div>
        {events.length > 0 && <span>{events.length} loaded events</span>}
      </div>
      {loaded && !events.length && <p>No retained events.</p>}
      <ol className={styles.timeline}>
        {events.map((event) => (
          <li key={`${event.title}:${event.id}`}>
            <time dateTime={event.at}>{formatMalaysianDateTime(event.at)}</time>
            <strong>{event.title}</strong>
            <p>{event.detail}</p>
            {event.kind === "inspection" && <span className={styles.actor}>{event.actor}</span>}
            {event.request && (
              <InspectionRepairLink
                inspectionId={id}
                selection={event.request}
                disabled={disabled}
                onSelect={onSelect}
              />
            )}
            {event.kind === "inspection" && (
              <div className={styles.eventActions}>
                <StaffLink
                  prefetch={false}
                  target="_blank"
                  rel="noopener"
                  href={`/dashboard/inspection/${id}/reports/${event.revision}`}
                >
                  View inspection report
                </StaffLink>
                {hasRepairs(event.revision) && (
                  <Button
                    size="compact"
                    variant="secondary"
                    disabled={disabled}
                    aria-expanded={previousRevision === event.revision}
                    onClick={() =>
                      selectRevision(previousRevision === event.revision ? 0 : event.revision)
                    }
                  >
                    {previousRevision === event.revision
                      ? "Hide repair requests"
                      : "View repair requests"}
                  </Button>
                )}
              </div>
            )}
            {event.kind === "inspection" &&
              hasRepairs(event.revision) &&
              previousRevision === event.revision && (
                <InspectionPreviousWork
                  revision={previousRevision}
                  read={selectedRepairs}
                  disabled={disabled}
                  onSelect={onSelect}
                />
              )}
            <details>
              <summary>Record details</summary>
              <p className={styles.reference}>Receipt: {event.id}</p>
              {event.kind !== "inspection" && (
                <p className={styles.reference}>Actor: {event.actor}</p>
              )}
            </details>
          </li>
        ))}
      </ol>
      <InspectionHistoryProgress read={revisions} label="Signed revisions" disabled={disabled} />
      {snapshot.draft.isSigned && (
        <InspectionHistoryProgress read={repairs} label="Repair requests" disabled={disabled} />
      )}
    </section>
  );
}
