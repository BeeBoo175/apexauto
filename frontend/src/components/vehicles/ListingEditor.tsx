"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { useListingEditor } from "@/hooks/use-listing-editor";
import { useListingPhotos } from "@/hooks/use-listing-photos";
import type { CurrentActor } from "@/lib/auth/actor";
import type { ListingWorkspace } from "@/lib/vehicles/listing-workspace";
import {
  listingRecordSection,
  type ListingRecordSelection,
} from "@/lib/vehicles/listing-history-selection";
import { listingPublication, type ListingSection } from "@/lib/vehicles/listings";
import { ListingPublication } from "./ListingPublication";
import { ListingPhotoPicker } from "./ListingPhotoPicker";
import { ListingDraftFields } from "./ListingDraftFields";
import { ListingEditingWorkspace } from "./ListingEditingWorkspace";
import { ListingEditorFooter } from "./ListingEditorFooter";
import { ListingHistory } from "./ListingHistory";
import { RetainedListingReview } from "./RetainedListingReview";
import workshop from "@/components/inspection/Workshop.module.css";
import styles from "./ListingEditor.module.css";

type PreviewTarget = "listing" | "card";

export function ListingEditor({
  snapshot,
  actor,
  section,
  changeSection,
  record,
  onRecord,
  onLock,
  onSaved,
}: {
  snapshot: ListingWorkspace;
  actor: CurrentActor;
  section: ListingSection;
  changeSection: (section: ListingSection) => void;
  record: ListingRecordSelection | null;
  onRecord: (record: ListingRecordSelection | null) => void;
  onLock: (locked: boolean) => void;
  onSaved: () => void;
}) {
  const editor = useListingEditor(snapshot, actor, onLock, onSaved);
  const photos = useListingPhotos(snapshot);
  const [mode, setMode] = useState<"edit" | "preview">(
    section === "card" ? "preview" : "edit",
  );
  const [lastTarget, setLastTarget] = useState<PreviewTarget>(
    section === "card" ? "card" : "listing",
  );
  const publication = section === "publication";
  const editing = section === "listing" || section === "card";
  const target = section === "listing" || section === "card" ? section : lastTarget;
  const historical = Boolean(
    record && listingRecordSection(record) === (editing ? "listing" : section),
  );
  const vehicle = {
    ...snapshot.vehicle,
    revisionId: editor.base?.id ?? null,
    version: editor.base?.version ?? 0,
  };

  function selectTarget(next: PreviewTarget) {
    setLastTarget(next);
    changeSection(next);
  }

  function viewCurrent() {
    onRecord(null);
    changeSection(publication ? "publication" : lastTarget);
  }

  return (
    <div className={workshop.workspace}>
      <LoadingProgress label="Uploading listing photo" active={editor.upload.busy} />
      <LoadingProgress label="Approving publication" active={editor.approval.busy} />
      <LoadingProgress label="Withdrawing publication" active={editor.withdrawal.busy} />
      {!historical && (
        <div className={styles.status}>
          <strong>{listingPublication(vehicle)}</strong>
          {(editing || section === "history") && (
            <span>{editor.base ? `Saved draft v${editor.base.version}` : "No saved draft"}</span>
          )}
        </div>
      )}
      <nav className={workshop.sections} aria-label="Listing workspace">
        <Button
          size="compact"
          variant="ghost"
          className={workshop.sectionButton}
          aria-pressed={editing}
          onClick={() => changeSection(lastTarget)}
        >
          Listing
        </Button>
        <Button
          size="compact"
          variant="ghost"
          className={workshop.sectionButton}
          aria-pressed={publication}
          onClick={() => {
            setLastTarget(target);
            changeSection("publication");
          }}
        >
          Publication
        </Button>
        <Button
          size="compact"
          variant="ghost"
          className={workshop.sectionButton}
          aria-pressed={section === "history"}
          onClick={() => {
            setLastTarget(target);
            changeSection("history");
          }}
        >
          History
        </Button>
      </nav>
      <div className={styles.body}>
        <div hidden={!editing || historical}>
          <ListingEditingWorkspace
            snapshot={snapshot}
            draft={editor.value}
            target={target}
            onTarget={selectTarget}
            mode={mode}
            onMode={setMode}
            fields={
              <ListingDraftFields
                value={editor.value}
                disabled={editor.operating}
                validationAttempted={editor.validationAttempted}
                onChange={editor.change}
              />
            }
            photos={
              <ListingPhotoPicker
                vehicleId={vehicle.vehicleId}
                vehicleName={`${vehicle.make} ${vehicle.model} ${vehicle.trim}`}
                editor={editor}
                photos={photos}
              />
            }
          />
        </div>
        <div hidden={!publication || historical}>
          <div className={styles.publication}>
            <ListingPublication snapshot={snapshot} editor={editor} />
          </div>
        </div>
        {historical && record && (
          <RetainedListingReview
            key={`${record.kind}:${record.eventId}:${record.revisionId}`}
            snapshot={snapshot}
            record={record}
          />
        )}
        <div hidden={section !== "history"}>
          <div className={styles.publication}>
            <ListingHistory snapshot={snapshot} active={section === "history"} onSelect={onRecord} />
          </div>
        </div>
      </div>
      <ListingEditorFooter
        editor={editor}
        section={section}
        historical={historical}
        onCurrent={viewCurrent}
      />
    </div>
  );
}
