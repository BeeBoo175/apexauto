"use client";

import { reportDocumentHref } from "@/lib/inspection/contracts";
import { useEffect, useRef } from "react";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { StaffLink } from "@/components/staff/StaffLink";
import type { useInspectionTimeline } from "@/hooks/use-inspection-timeline";
import { inspectionTimeline } from "@/lib/inspection/timeline";
import type { EditorSnapshot } from "@/lib/inspection/editor-snapshot";
import type { RepairSelection } from "@/lib/inspection/query";
import styles from "@/components/staff/StaffHistory.module.css";
import { StaffTimeline, StaffTimelineEvent } from "@/components/staff/StaffTimeline";
import { StaffHistorySection } from "@/components/staff/StaffHistorySection";
import { StaffHistoryProgress } from "@/components/staff/StaffHistoryProgress";
import { InspectionRepairLink } from "./InspectionHistory";

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
  const rail = useRef<HTMLOListElement>(null);
  const revealRevision = useRef(0);
  const id = snapshot.draft.id;
  const { revisions, repairs, previous, previousRevision, selectRevision } = timeline;
  const revealed = previousRevision > 0 ? previous.history : null;
  const events = inspectionTimeline(revisions.history, repairs.history, revealed);
  const hasRepairs = (revision: number) =>
    revisions.history?.items.some((item) => item.revision === revision && item.hasRepairRequests);

  useEffect(() => {
    if (
      !revealed ||
      previous.busy ||
      previous.failed ||
      revealRevision.current !== revealed.revision
    ) {
      return;
    }
    revealRevision.current = 0;
    rail.current
      ?.querySelector<HTMLElement>(`[data-repair-revision="${revealed.revision}"]`)
      ?.focus();
  }, [revealed, previous.busy, previous.failed]);

  return (
    <StaffHistorySection
      label="Record history"
      busy={revisions.busy || repairs.busy || previous.busy}
      empty={revisions.history?.totalCount === 0 && <p>No signed inspections yet.</p>}
      progress={
        <>
          <StaffHistoryProgress read={revisions} label="Signed inspections" disabled={disabled} />
          {snapshot.draft.isSigned && (
            <StaffHistoryProgress read={repairs} label="Repair requests" disabled={disabled} />
          )}
        </>
      }
    >
      <StaffTimeline listRef={rail}>
        {events.map((event) => {
          const signed = event.kind === "inspection";
          const current = event.revision === snapshot.draft.revision;
          const expanded = previousRevision === event.revision;
          return (
            <StaffTimelineEvent
              key={`${event.kind}:${event.id}`}
              title={event.title}
              at={event.at}
              context={event.context}
              data-repair-revision={
                signed || event.kind === "reopening" ? undefined : event.revision
              }
              tabIndex={-1}
            >
              {event.detail && (
                <p className={event.kind === "request" ? styles.workSummary : undefined}>
                  {event.detail}
                </p>
              )}
              {event.request && (
                <InspectionRepairLink
                  inspectionId={id}
                  selection={event.request}
                  disabled={disabled}
                  onSelect={onSelect}
                />
              )}
              {signed && (
                <div className={styles.eventActions}>
                  <StaffLink
                    prefetch={false}
                    target="_blank"
                    rel="noopener"
                    href={reportDocumentHref(id, event.revision)}
                  >
                    View inspection report
                  </StaffLink>
                  {!current && hasRepairs(event.revision) && (
                    <Button
                      size="compact"
                      variant="ghost"
                      className={styles.disclosure}
                      disabled={disabled}
                      aria-expanded={expanded}
                      onClick={() => {
                        revealRevision.current = expanded ? 0 : event.revision;
                        selectRevision(expanded ? 0 : event.revision);
                      }}
                    >
                      <Icon name="down" />
                      {expanded ? "Hide repair history" : "Show repair history"}
                    </Button>
                  )}
                </div>
              )}
              {signed && !current && expanded && (
                <StaffHistoryProgress read={previous} label="Repair requests" disabled={disabled} />
              )}
            </StaffTimelineEvent>
          );
        })}
      </StaffTimeline>
    </StaffHistorySection>
  );
}
