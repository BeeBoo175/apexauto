"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { useListingEditor } from "@/hooks/use-listing-editor";
import { useListingPhotos } from "@/hooks/use-listing-photos";
import type { CurrentActor } from "@/lib/auth/actor";
import type { ListingWorkspace } from "@/lib/vehicles/listing-workspace";
import { listingPublication, type ListingSection } from "@/lib/vehicles/listings";
import { ListingPublication } from "./ListingPublication";
import { ListingPhotoPicker } from "./ListingPhotoPicker";
import { ListingDraftFields } from "./ListingDraftFields";
import { ListingEditingWorkspace } from "./ListingEditingWorkspace";
import { ListingEditorFooter } from "./ListingEditorFooter";
import workshop from "@/components/inspection/Workshop.module.css";
import styles from "./ListingEditor.module.css";

type PreviewTarget = "listing" | "card";

export function ListingEditor({
  snapshot,
  actor,
  section,
  changeSection,
  onLock,
  onSaved,
}: {
  snapshot: ListingWorkspace;
  actor: CurrentActor;
  section: ListingSection;
  changeSection: (section: ListingSection) => void;
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
  const vehicle = {
    ...snapshot.vehicle,
    revisionId: editor.base?.id ?? null,
    version: editor.base?.version ?? 0,
  };

  function selectTarget(next: PreviewTarget) {
    setLastTarget(next);
    changeSection(next);
  }

  return (
    <div className={workshop.workspace}>
      <LoadingProgress label="Uploading listing photo" active={editor.upload.busy} />
      <LoadingProgress label="Approving publication" active={editor.approval.busy} />
      <LoadingProgress label="Withdrawing publication" active={editor.withdrawal.busy} />
      {(
        <div className={styles.status}>
          <strong>{listingPublication(vehicle)}</strong>
          {editing && (
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
      </nav>
      <div className={styles.body}>
        <div hidden={!editing}>
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
        <div hidden={!publication}>
          <div className={styles.publication}>
            <ListingPublication snapshot={snapshot} editor={editor} />
          </div>
        </div>
      </div>
      <ListingEditorFooter
        editor={editor}
        section={section}
      />
    </div>
  );
}