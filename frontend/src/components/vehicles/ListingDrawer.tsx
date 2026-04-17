"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { WorkshopLoadingShell } from "@/components/inspection/WorkshopLoadingShell";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import type { useStaffSelection } from "@/hooks/use-staff-selection";
import type { ListingWorkspace } from "@/lib/vehicles/listing-workspace";
import type { ListingSection } from "@/lib/vehicles/listings";
import {
  listingRecordQuery,
  listingRecordSection,
  type ListingRecordSelection,
} from "@/lib/vehicles/listing-history-selection";
import type { ApiResult } from "@/lib/api";
import { ListingEditor } from "./ListingEditor";
import workbench from "@/components/staff/StaffWorkspace.module.css";

export function ListingDrawer({
  actor,
  onSaved,
  selection,
  initial,
  initialSection,
  initialRecord = null,
}: {
  actor: CurrentActor;
  onSaved: () => void;
  selection: ReturnType<typeof useStaffSelection<ListingWorkspace>>;
  initial: ApiResult<ListingWorkspace> | null;
  initialSection: ListingSection;
  initialRecord?: ListingRecordSelection | null;
}) {
  const [locked, setLocked] = useState(false);
  const [section, setSection] = useState(initialSection);
  const [record, setRecord] = useState(initialRecord);
  const [entry, setEntry] = useState(initial);
  if (entry !== initial) {
    setEntry(initial);
    setSection(initialSection);
    setRecord(initialRecord);
  }
  const snapshot = selection.result?.data;
  const vehicle = snapshot?.vehicle;

  function changeSection(next: ListingSection) {
    setSection(next);
    const url = new URL(window.location.href);
    url.searchParams.set("section", next);
    window.history.replaceState(null, "", url);
  }

  function close() {
    if (locked) {
      return;
    }
    selection.close();
    setSection("listing");
    setRecord(null);
    const url = new URL(window.location.href);
    url.searchParams.delete("section");
    url.searchParams.delete("revision");
    url.searchParams.delete("event");
    url.searchParams.delete("eventKind");
    window.history.replaceState(null, "", url);
  }

  function review(next: ListingRecordSelection | null) {
    setRecord(next);
    const url = new URL(window.location.href);
    url.searchParams.delete("revision");
    url.searchParams.delete("event");
    url.searchParams.delete("eventKind");
    if (next) {
      setSection(listingRecordSection(next));
      listingRecordQuery(next).forEach((value, key) => url.searchParams.set(key, value));
    }
    window.history.replaceState(null, "", url);
  }

  return (
    <>
      <LoadingProgress label="Loading listing workspace" active={selection.busy} />
      <Dialog
        open={Boolean(selection.id || selection.pendingId)}
        variant="drawer"
        density="compact"
        width="wide"
        layout="workspace"
        title={vehicle?.registrationPlate ?? "Listing"}
        description={
          vehicle ? `${vehicle.make} ${vehicle.model} · ${vehicle.branchName}` : undefined
        }
        onClose={close}
        closeDisabled={locked}
        headerActions={
          <RefreshButton
            label="Refresh listing"
            busy={selection.busy}
            disabled={!selection.id}
            onClick={() => void selection.choose(selection.id)}
          />
        }
      >
        {selection.busy && !snapshot && <WorkshopLoadingShell label="Loading listing…" />}
        {!selection.busy && !snapshot && (
          <div className={workbench.protocol}>
            <p>The listing is unavailable. Your table remains unchanged.</p>
            <Button
              size="compact"
              variant="secondary"
              onClick={() => void selection.choose(selection.id)}
            >
              Retry listing
            </Button>
          </div>
        )}
        {snapshot && vehicle && (
          <ListingEditor
            key={`${actorIdentity(actor)}:${vehicle.vehicleId}:${vehicle.branchId}`}
            snapshot={snapshot}
            actor={actor}
            section={section}
            changeSection={changeSection}
            record={record}
            onRecord={review}
            onLock={setLocked}
            onSaved={() => {
              onSaved();
              void selection.choose(selection.id);
            }}
          />
        )}
      </Dialog>
    </>
  );
}
