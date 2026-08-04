"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { ListingLoadingShell } from "./ListingLoadingShell";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import type { useListingSelection } from "@/hooks/use-listing-selection";
import { ListingEditor } from "./ListingEditor";
import workbench from "@/components/staff/StaffWorkspace.module.css";

export function ListingDrawer({
  actor,
  onSaved,
  selection,
}: {
  actor: CurrentActor;
  onSaved: () => void;
  selection: ReturnType<typeof useListingSelection>;
}) {
  const [locked, setLocked] = useState(false);
  const { section, record, changeSection, review: onRecord } = selection;
  const snapshot = selection.result?.data;
  const vehicle = snapshot?.vehicle;

  return (
    <>
      <LoadingProgress label="Loading listing workspace" active={selection.busy} />
      <Dialog
        open={Boolean(selection.id || selection.pendingId)}
        variant="drawer"
        density="compact"
        width="wide"
        title={vehicle?.registrationPlate ?? "Listing"}
        headerLoading={selection.busy && !snapshot}
        description={
          vehicle ? `${vehicle.make} ${vehicle.model} · ${vehicle.branchName}` : undefined
        }
        onClose={selection.close}
        headerActions={
          <RefreshButton
            label="Refresh listing"
            busy={selection.busy}
            disabled={!selection.id || locked}
            onClick={() => void selection.choose(selection.id)}
          />
        }
      >
        {selection.busy && !snapshot && <ListingLoadingShell section={section} />}
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
            onRecord={onRecord}
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
