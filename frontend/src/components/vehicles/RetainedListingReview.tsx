"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { AppointmentLoadingShell } from "@/components/staff/AppointmentLoadingShell";
import { useListingRevision } from "@/hooks/use-listing-revision";
import { listingDraft } from "@/lib/vehicles/listing-draft";
import type { ListingRecordSelection } from "@/lib/vehicles/listing-history-selection";
import type { ListingWorkspace } from "@/lib/vehicles/listing-workspace";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { ListingEditingWorkspace } from "./ListingEditingWorkspace";
import { ListingDraftFields } from "./ListingDraftFields";
import { ListingGallery } from "./ListingGallery";
import { ListingPublicationRecord } from "./ListingPublicationRecord";
import styles from "./ListingEditor.module.css";

const recordDetails = ["Saved listing", "Retained record"];

export function RetainedListingReview({
  snapshot,
  record,
}: {
  snapshot: ListingWorkspace;
  record: ListingRecordSelection;
}) {
  const [target, setTarget] = useState<"listing" | "card">("listing");
  const [mode, setMode] = useState<"edit" | "preview">("edit");
  const read = useListingRevision(snapshot.vehicle.vehicleId, snapshot.vehicle.branchId, record);
  const saved = read.result;
  const revision = saved?.revision;
  const draft = revision ? listingDraft(revision) : null;
  const publication = record.kind !== "DraftSaved";
  const publicationLabel =
    record.kind === "ListingWithdrawn" ? "Listing withdrawal" : "Publication approval";

  return (
    <section
      className={styles.tabContent}
      aria-label={publication ? "Historical publication" : "Historical listing"}
    >
      <LoadingProgress label="Loading historical listing record" active={read.busy} />
      {read.busy && !saved && (
        <AppointmentLoadingShell label="Loading saved record…" details={recordDetails} />
      )}
      {!read.busy && !saved && (
        <div className={styles.publication}>
          <p role="alert">This saved record is unavailable. Your current draft is unchanged.</p>
          <Button size="compact" variant="secondary" onClick={read.retry}>
            Retry saved record
          </Button>
        </div>
      )}
      {saved && revision && draft && (
        <>
          <div className={styles.historicalHeading}>
            {publication ? (
              <strong>{publicationLabel} · Read only</strong>
            ) : (
              <>
                <strong>Revision {revision.version} · Read only</strong>
                <span>Saved {formatMalaysianDateTime(revision.recordedAt)}</span>
              </>
            )}
          </div>
          {publication ? (
            <div className={styles.publication}>
              <ListingPublicationRecord
                revision={revision}
                publication={saved.publication}
                withdrawal={record.kind === "ListingWithdrawn" ? saved.withdrawal : null}
                signedReport={saved.signedReport}
              />
            </div>
          ) : (
            <ListingEditingWorkspace
              snapshot={snapshot}
              draft={draft}
              target={target}
              onTarget={setTarget}
              mode={mode}
              onMode={setMode}
              signedReport={saved.signedReport}
              fields={<ListingDraftFields value={draft} readOnly />}
              photos={
                <section>
                  <h3>Photos</h3>
                  <ListingGallery
                    vehicleId={snapshot.vehicle.vehicleId}
                    vehicleName={`${snapshot.vehicle.make} ${snapshot.vehicle.model} ${snapshot.vehicle.trim}`}
                    photoIds={draft.photoIds}
                  />
                </section>
              }
            />
          )}
        </>
      )}
    </section>
  );
}
