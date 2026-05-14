"use client";

import { useState } from "react";
import { Dialog } from "@/components/ui/Dialog";
import { useEditGuard } from "@/hooks/use-edit-guard";
import type { useTechnicianQueue } from "@/hooks/use-technician-queue";
import type { CurrentActor } from "@/lib/auth/actor";
import type { WorkshopRow } from "@/lib/inspection/workshop-contracts";
import { AssignmentForm } from "./AssignmentForm";
import { TechnicianJobList } from "./TechnicianJobList";
import styles from "@/components/staff/StaffQueueWorkspace.module.css";

export function TechnicianClaim({
  actor,
  state,
  selectionLocked,
  onClaimed,
}: {
  actor: CurrentActor;
  state: ReturnType<typeof useTechnicianQueue>;
  selectionLocked: boolean;
  onClaimed: (id: string) => void;
}) {
  const [vehicle, setVehicle] = useState<WorkshopRow | null>(null);
  const [locked, setLocked] = useState(false);
  const lockNavigation = useEditGuard(locked);
  return (
    <>
      <div className={styles.queueList} inert={Boolean(vehicle)}>
        <TechnicianJobList
          title="Available to claim"
          state={state}
          claim
          selectionLocked={selectionLocked}
          onOpen={setVehicle}
        />
      </div>
      <Dialog
        open={Boolean(vehicle)}
        title={`Claim inspection · ${vehicle?.registrationPlate ?? ""}`}
        closeDisabled={locked}
        onClose={() => setVehicle(null)}
      >
        {vehicle && (
          <AssignmentForm
            key={vehicle.vehicleId}
            actor={actor}
            vehicleId={vehicle.vehicleId}
            version={0}
            initialChoices={null}
            onLocked={setLocked}
            onConfirmed={(id) => {
              lockNavigation(false);
              setLocked(false);
              setVehicle(null);
              onClaimed(id);
            }}
          />
        )}
      </Dialog>
    </>
  );
}
