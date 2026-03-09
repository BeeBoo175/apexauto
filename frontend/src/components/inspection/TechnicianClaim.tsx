"use client";

import { useState } from "react";
import { Dialog } from "@/components/ui/Dialog";
import { useInspectionLock } from "@/hooks/use-inspection-lock";
import type { useInspectionQueue } from "@/hooks/use-inspection-queue";
import type { CurrentActor } from "@/lib/auth/actor";
import type { WorkshopRow } from "@/lib/inspection/workshop-contracts";
import { AssignmentForm } from "./AssignmentForm";
import { TechnicianJobList } from "./TechnicianJobList";

export function TechnicianClaim({
  actor,
  state,
  onClaimed,
}: {
  actor: CurrentActor;
  state: ReturnType<typeof useInspectionQueue<WorkshopRow>>;
  onClaimed: (id: string) => void;
}) {
  const [vehicle, setVehicle] = useState<WorkshopRow | null>(null);
  const [locked, setLocked] = useState(false);
  const lockNavigation = useInspectionLock(locked);
  return (
    <>
      <div inert={Boolean(vehicle)}>
        <TechnicianJobList title="Claim inspection" state={state} claim onOpen={setVehicle} />
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
