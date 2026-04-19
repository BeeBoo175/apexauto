import { useState } from "react";
import { useLoadingProgress } from "@/components/ui/LoadingProgress";
import { Dialog, DialogActionSlot } from "@/components/ui/Dialog";
import { AppointmentWorkbench } from "./AppointmentWorkbench";
import { AppointmentLoadingShell } from "./AppointmentLoadingShell";
import type { CurrentActor } from "@/lib/auth/actor";
import type { useStaffAppointmentDrawer } from "@/hooks/use-staff-appointment-drawer";

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
      headerActions={<DialogActionSlot ref={setHeaderActions} />}
      onClose={drawer.close}
      closeDisabled={drawer.locked}
    >
      {drawer.busy && <AppointmentLoadingShell />}
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
