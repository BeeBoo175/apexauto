"use client";

import { useState } from "react";
import { Dialog, DialogActionSlot } from "@/components/ui/Dialog";
import { Button } from "@/components/ui/Button";
import { AppointmentLoadingShell } from "@/components/staff/AppointmentLoadingShell";
import type { useInspectionDrawer } from "@/hooks/use-inspection-drawer";
import type { CurrentActor } from "@/lib/auth/actor";
import { InspectionEditor } from "./InspectionEditor";
import workbench from "@/components/staff/StaffWorkspace.module.css";

const inspectionDetails = ["Branch", "Vehicle", "VIN", "Revision", "Mileage", "Saved assessment"];

export function InspectionDrawer({
  actor,
  selection,
  repairs = false,
}: {
  actor: CurrentActor;
  selection: ReturnType<typeof useInspectionDrawer>;
  repairs?: boolean;
}) {
  const [headerActions, setHeaderActions] = useState<HTMLDivElement | null>(null);

  return (
    <Dialog
      open={Boolean(selection.id)}
      variant="drawer"
      density="compact"
      width={repairs ? "default" : "wide"}
      title={repairs ? "Repair request" : "Inspection"}
      headerActions={<DialogActionSlot ref={setHeaderActions} />}
      closeDisabled={selection.locked}
      onClose={selection.close}
    >
      {selection.busy && (
        <AppointmentLoadingShell
          label={repairs ? "Loading repair request…" : "Loading inspection…"}
          details={inspectionDetails}
        />
      )}
      {!selection.busy && selection.result?.data && (
        <InspectionEditor
          key={selection.id}
          id={selection.id}
          actor={actor}
          initial={selection.result}
          technicians={null}
          actionsContainer={headerActions}
          repairsOnly={repairs}
          selectedRequest={selection.request}
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
  );
}
