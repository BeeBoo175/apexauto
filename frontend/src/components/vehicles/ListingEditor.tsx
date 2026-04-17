"use client";

import { useState } from "react";
import { StaffWorkspaceTabs } from "@/components/staff/StaffWorkspaceTabs";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { useListingEditor } from "@/hooks/use-listing-editor";
import { useListingEstimate } from "@/hooks/use-listing-estimate";
import { useListingPhotos } from "@/hooks/use-listing-photos";
import { useWorkspaceTabs } from "@/hooks/use-workspace-tabs";
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
import { ListingEstimateSummary } from "./ListingEstimateSummary";
import { ListingEditorFooter } from "./ListingEditorFooter";
import { ListingHistory } from "./ListingHistory";
import { RetainedListingReview } from "./RetainedListingReview";
import workshop from "@/components/inspection/Workshop.module.css";
import styles from "./ListingEditor.module.css";

type PreviewTarget = "listing" | "card";

export function ListingEditor({
  snapshot,
  actor,
  section: requestedSection,
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
    requestedSection === "card" ? "preview" : "edit",
  );
  const [lastTarget, setLastTarget] = useState<PreviewTarget>(
    requestedSection === "card" ? "card" : "listing",
  );
  const listingTarget =
    requestedSection === "listing" || requestedSection === "card" ? requestedSection : lastTarget;
  const publicationAvailable = Boolean(
    editor.base ||
    snapshot.publication ||
    editor.approval.busy ||
    editor.approval.uncertain ||
    editor.approval.receipt ||
    editor.withdrawal.busy ||
    editor.withdrawal.uncertain ||
    editor.withdrawal.receipt ||
    editor.withdrawalReason ||
    (record && record.kind !== "DraftSaved"),
  );
  const historyAvailable = Boolean(editor.base || snapshot.draft || snapshot.publication || record);
  const { section, tabs } = useWorkspaceTabs<ListingSection>(
    requestedSection,
    [
      { value: listingTarget, label: "Listing", available: true },
      { value: "publication", label: "Publication", available: publicationAvailable },
      { value: "history", label: "History", available: historyAvailable },
    ],
    listingTarget,
    changeSection,
  );
  const publication = section === "publication";
  const editing = section === "listing" || section === "card";
  const target = section === "listing" || section === "card" ? section : lastTarget;
  const historical = Boolean(
    record && listingRecordSection(record) === (editing ? "listing" : section),
  );
  const estimate = useListingEstimate(
    snapshot,
    actor,
    editor.value,
    editing && !historical && target === "listing",
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
      <StaffWorkspaceTabs
        label="Listing workspace"
        tabs={tabs}
        value={section}
        onChange={(next) => {
          setLastTarget(target);
          changeSection(next);
        }}
      />
      <div className={styles.body} data-split={editing || undefined}>
        <div className={styles.tabContent} hidden={!editing || historical}>
          <ListingEditingWorkspace
            snapshot={snapshot}
            draft={editor.value}
            target={target}
            onTarget={selectTarget}
            mode={mode}
            onMode={setMode}
            purchaseSummary={
              <ListingEstimateSummary
                estimate={estimate.estimate}
                roadTax={editor.value.roadTax}
                incomplete={estimate.incomplete}
                busy={estimate.busy}
                failed={estimate.failed}
                onRetry={estimate.retry}
              />
            }
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
        <div className={styles.tabContent} hidden={!publication || historical}>
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
        <div className={styles.tabContent} hidden={section !== "history"}>
          <div className={styles.publication}>
            <ListingHistory
              snapshot={snapshot}
              active={section === "history"}
              onSelect={onRecord}
            />
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
