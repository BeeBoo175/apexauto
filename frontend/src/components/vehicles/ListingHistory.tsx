"use client";

import { StaffHistorySection } from "@/components/staff/StaffHistorySection";
import { StaffTimeline, StaffTimelineEvent } from "@/components/staff/StaffTimeline";
import { PersonLabel } from "@/components/staff/PersonLabel";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { StaffLink } from "@/components/staff/StaffLink";
import { StaffHistoryProgress } from "@/components/staff/StaffHistoryProgress";
import { useListingHistory } from "@/hooks/use-listing-history";
import type { ListingHistoryEntry } from "@/lib/vehicles/listing-history";
import {
  listingRecordQuery,
  type ListingRecordSelection,
} from "@/lib/vehicles/listing-history-selection";
import type { ListingWorkspace } from "@/lib/vehicles/listing-workspace";
import styles from "@/components/staff/StaffHistory.module.css";

const eventTitles: Record<ListingHistoryEntry["kind"], string> = {
  DraftSaved: "Listing draft saved",
  PublicationApproved: "Publication approved",
  ListingWithdrawn: "Listing withdrawn",
};

export function ListingHistory({
  snapshot,
  active,
  onSelect,
}: {
  snapshot: ListingWorkspace;
  active: boolean;
  onSelect: (record: ListingRecordSelection) => void;
}) {
  const read = useListingHistory(snapshot, active);

  return (
    <StaffHistorySection
      label="Listing history"
      busy={read.busy}
      empty={read.history?.totalCount === 0 && <p>No listing revisions have been saved yet.</p>}
      progress={<StaffHistoryProgress read={read} label="Listing records" />}
    >
      <LoadingProgress label="Loading listing history" active={active && read.busy} />
      <StaffTimeline>
        {read.history?.items.map((entry) => {
          const record = { kind: entry.kind, revisionId: entry.revisionId, eventId: entry.id };
          const query = listingRecordQuery(record);
          query.set("listing", snapshot.vehicle.vehicleId);
          return (
            <StaffTimelineEvent
              key={`${entry.kind}:${entry.id}`}
              title={eventTitles[entry.kind]}
              at={entry.recordedAt}
              context={
                <>
                  Revision {entry.version} ·{" "}
                  <PersonLabel
                    id={entry.actorId}
                    name={entry.actorIdentity}
                    identity={entry.actorIdentity}
                    pictureId={entry.actorPictureId}
                  />
                </>
              }
            >
              {entry.reason && <p>{entry.reason}</p>}
              <div className={styles.eventActions}>
                <StaffLink
                  prefetch={false}
                  href={`/dashboard/management?${query}`}
                  onClick={(event) => {
                    if (
                      event.button !== 0 ||
                      event.ctrlKey ||
                      event.metaKey ||
                      event.shiftKey ||
                      event.altKey
                    ) {
                      return;
                    }
                    event.preventDefault();
                    onSelect(record);
                  }}
                >
                  {entry.kind === "DraftSaved"
                    ? "View listing revision"
                    : "View publication record"}
                </StaffLink>
              </div>
            </StaffTimelineEvent>
          );
        })}
      </StaffTimeline>
    </StaffHistorySection>
  );
}
