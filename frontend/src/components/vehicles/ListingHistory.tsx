"use client";

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
import { formatMalaysianDateTime } from "@/lib/date-format";
import styles from "@/components/staff/StaffHistory.module.css";
import editorStyles from "./ListingEditor.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";

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
    <section
      className={editorStyles.historyContent}
      aria-label="Listing history"
      aria-busy={read.busy}
    >
      <LoadingProgress label="Loading listing history" active={active && read.busy} />
      <div className={workbench.sectionHeader}>
        <h2 className={workbench.sectionTitle}>Record history</h2>
      </div>
      {read.history?.totalCount === 0 && <p>No listing revisions have been saved yet.</p>}
      <ol className={styles.timeline}>
        {read.history?.items.map((entry) => {
          const record = { kind: entry.kind, revisionId: entry.revisionId, eventId: entry.id };
          const query = listingRecordQuery(record);
          query.set("listing", snapshot.vehicle.vehicleId);
          return (
            <li key={`${entry.kind}:${entry.id}`}>
              <div className={styles.eventHeading}>
                <h3>{eventTitles[entry.kind]}</h3>
                <time dateTime={entry.recordedAt}>{formatMalaysianDateTime(entry.recordedAt)}</time>
              </div>
              <span className={styles.context}>
                Revision {entry.version} · {entry.actorIdentity}
              </span>
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
            </li>
          );
        })}
      </ol>
      <StaffHistoryProgress read={read} label="Listing records" />
    </section>
  );
}
