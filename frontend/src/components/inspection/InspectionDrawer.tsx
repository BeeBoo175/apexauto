"use client";

import { useState } from "react";
import { ReloadNotice } from "@/components/ui/ReloadNotice";
import { Dialog, DialogActionSlot } from "@/components/ui/Dialog";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { StaffDrawerLoadingShell } from "@/components/staff/StaffDrawerLoadingShell";
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
        title={vehicle?.registrationPlate ?? "Workshop"}
        headerLoading={selection.busy && !selection.result?.data}
        description={
          vehicle ? `${vehicle.make} ${vehicle.model} · ${vehicle.branchName}` : undefined
        }
        headerActions={<DialogActionSlot ref={setHeaderActions} />}
        onClose={selection.close}
      >
        {selection.busy && !selection.result?.data && (
          <StaffDrawerLoadingShell
            label="Loading workshop record…"
            section={selection.section}
            fact
          />
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
            category={selection.category}
            onCategory={selection.changeCategory}
            onReview={selection.review}
            selectedRequest={selection.request}
            selectedReview={selectedReview}
            onLocked={selection.lock}
            onChanged={selection.markChanged}
          />
        )}
        {!selection.busy && !selection.result?.data && selection.id && (
          <div className={workbench.protocol}>
            <ReloadNotice subject="Inspection" />
          </div>
        )}
      </Dialog>
    </>
  );
}
