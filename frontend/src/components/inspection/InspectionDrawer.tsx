"use client";

import { useState } from "react";
import { Dialog, DialogActionSlot } from "@/components/ui/Dialog";
import { Button } from "@/components/ui/Button";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { WorkshopLoadingShell } from "./WorkshopLoadingShell";
import type { useInspectionDrawer } from "@/hooks/use-inspection-drawer";
import type { CurrentActor } from "@/lib/auth/actor";
import { InspectionEditor } from "./InspectionEditor";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import type { SelectedRepairReview } from "@/lib/inspection/repair-review";

export function InspectionDrawer({
  actor,
  selection,
  selectedReview = null,
}: {
  actor: CurrentActor;
  selection: ReturnType<typeof useInspectionDrawer>;
  selectedReview?: SelectedRepairReview | null;
}) {
  const [headerActions, setHeaderActions] = useState<HTMLDivElement | null>(null);
  const vehicle = selection.result?.data?.draft.vehicle;

  return (
    <>
      <LoadingProgress label="Opening workshop job" active={selection.busy} />
      <Dialog
        open={Boolean(selection.id || selection.pendingId)}
        variant="drawer"
        density="compact"
        width="wide"
        layout="workspace"
        title={vehicle?.registrationPlate ?? "Workshop"}
        headerLoading={selection.busy && !selection.result?.data}
        description={
          vehicle ? `${vehicle.make} ${vehicle.model} · ${vehicle.branchName}` : undefined
        }
        headerActions={<DialogActionSlot ref={setHeaderActions} />}
        closeDisabled={selection.locked}
        onClose={selection.close}
      >
        {selection.busy && !selection.result?.data && (
          <WorkshopLoadingShell section={selection.section} />
        )}
        {selection.result?.data && (
          <InspectionEditor
            key={selection.id}
            id={selection.id}
            actor={actor}
            initial={selection.result}
            technicians={null}
            actionsContainer={headerActions}
            section={selection.section}
            onSection={selection.changeSection}
            onReview={selection.review}
            selectedRequest={selection.request}
            selectedReview={selectedReview}
            onLocked={selection.lock}
            onChanged={selection.markChanged}
          />
        )}
        {!selection.busy && !selection.result?.data && selection.id && (
          <div className={workbench.protocol}>
            <p>The inspection is unavailable. Your list remains unchanged.</p>
            <Button
              size="compact"
              variant="secondary"
              onClick={() => void selection.choose(selection.id)}
            >
              Retry inspection
            </Button>
          </div>
        )}
      </Dialog>
    </>
  );
}
