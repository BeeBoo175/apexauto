import { useState } from "react";
import { useLoadingProgress } from "@/components/ui/LoadingProgress";
import { Dialog, DialogActionSlot } from "@/components/ui/Dialog";
import { AppointmentWorkbench } from "./AppointmentWorkbench";
import { StaffRecordLoadingShell } from "./StaffRecordLoadingShell";
import type { CurrentActor } from "@/lib/auth/actor";
import type { useStaffAppointmentDrawer } from "@/hooks/use-staff-appointment-drawer";

const appointmentDetails = [
  "Branch",
  "Customer",
  "Inspection",
  "Booked vehicle",
  "Declared mileage",
  "Reference",
];

export function IntakeAppointmentDrawer({
  actor,
  drawer,
  onConfirmed,
}: {
  actor: CurrentActor;
  drawer: ReturnType<typeof useStaffAppointmentDrawer>;
  onConfirmed: () => void;
}) {
  const [headerActions, setHeaderActions] = useState<HTMLDivElement | null>(null);
  useLoadingProgress("Loading intake appointment", drawer.busy);

  return (
    <Dialog
      open={Boolean(drawer.id)}
      variant="drawer"
      density="compact"
      title="Intake appointment"
      headerLoading={drawer.busy}
      descriptionPlaceholder={false}
      description={
        drawer.result?.data
          ? `${drawer.result.data.registrationPlate} · ${drawer.result.data.manufactureYear} ${drawer.result.data.make} ${drawer.result.data.model} ${drawer.result.data.trim} · ${drawer.result.data.branchName}`
          : undefined
      }
      headerActions={<DialogActionSlot ref={setHeaderActions} />}
      onClose={drawer.close}
    >
      {drawer.busy && (
        <StaffRecordLoadingShell
          label="Loading appointment…"
          details={appointmentDetails}
          showForm={actor.role !== "GeneralManager"}
        />
      )}
      {!drawer.busy && drawer.result && (
        <AppointmentWorkbench
          key={drawer.id}
          id={drawer.id}
          actor={actor}
          initial={drawer.result}
          actionsContainer={headerActions}
          draft={drawer.draft}
          onDraft={drawer.setDraft}
          onLock={drawer.lock}
          onConfirmed={onConfirmed}
        />
      )}
    </Dialog>
  );
}
