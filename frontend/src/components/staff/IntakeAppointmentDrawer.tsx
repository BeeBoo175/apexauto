import { useState } from "react";
import { Dialog, DialogActionSlot } from "@/components/ui/Dialog";
import { AppointmentWorkbench } from "./AppointmentWorkbench";
import { AppointmentLoadingShell } from "./AppointmentLoadingShell";
import type { CurrentActor } from "@/lib/auth/actor";
import type { useStaffAppointmentDrawer } from "@/hooks/use-staff-appointment-drawer";

export function IntakeAppointmentDrawer({
  actor,
  drawer,
  refreshing,
  onConfirmed,
}: {
  actor: CurrentActor;
  drawer: ReturnType<typeof useStaffAppointmentDrawer>;
  refreshing: boolean;
  onConfirmed: () => void;
}) {
  const [headerActions, setHeaderActions] = useState<HTMLDivElement | null>(null);

  return (
    <Dialog
      open={Boolean(drawer.id)}
      variant="drawer"
      density="compact"
      title="Intake appointment"
      headerActions={<DialogActionSlot ref={setHeaderActions} />}
      onClose={drawer.close}
      closeDisabled={drawer.locked || refreshing}
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
