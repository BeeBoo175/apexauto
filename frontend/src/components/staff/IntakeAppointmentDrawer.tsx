import { useState } from "react";
import { useLoadingProgress } from "@/components/ui/LoadingProgress";
import { Dialog, DialogActionSlot } from "@/components/ui/Dialog";
import { AppointmentWorkbench } from "./AppointmentWorkbench";
import { StaffDrawerLoadingShell } from "./StaffDrawerLoadingShell";
import type { CurrentActor } from "@/lib/auth/actor";
import type { useStaffAppointmentDrawer } from "@/hooks/use-staff-appointment-drawer";

export function IntakeAppointmentDrawer({
  actor,
  drawer,
  onConfirmed,
  retryTraversal,
}: {
  actor: CurrentActor;
  drawer: ReturnType<typeof useStaffAppointmentDrawer>;
  onConfirmed: () => void;
  retryTraversal: () => boolean;
}) {
  const [headerActions, setHeaderActions] = useState<HTMLDivElement | null>(null);
  useLoadingProgress("Loading intake appointment", drawer.busy);

  return (
    <Dialog
      open={Boolean(drawer.id || drawer.pendingId)}
      variant="drawer"
      density="compact"
      title="Intake appointment"
      headerLoading={drawer.busy}
      description={
        drawer.result?.data
          ? `${drawer.result.data.registrationPlate} · ${drawer.result.data.manufactureYear} ${drawer.result.data.make} ${drawer.result.data.model} ${drawer.result.data.trim} · ${drawer.result.data.branchName}`
          : undefined
      }
      headerActions={<DialogActionSlot ref={setHeaderActions} />}
      onClose={drawer.close}
    >
      {drawer.busy && (
        <StaffDrawerLoadingShell label="Loading appointment…" fact single tabs={false} />
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
          retryTraversal={retryTraversal}
        />
      )}
    </Dialog>
  );
}
